using System;

namespace MinorProject.Services;

public enum DistributionStrategy
{
    T1First,
    T2First,
    Proportional
}

public enum PowerTransferDirection
{
    T1ToT2,
    T2ToT1
}

public class LoadBalancerService
{
    public (double t1Power, double t2Power, double appliedTransfer, string warning) Distribute(
        double totalDemand,
        bool t1Available, bool t2Available,
        bool t1Manual, bool t2Manual,
        double t1ManualPower, double t2ManualPower,
        double balancerMaxT1, double balancerMaxT2,
        double t2Threshold,
        DistributionStrategy strategy,
        bool manualTransferEnabled,
        PowerTransferDirection transferDirection,
        double transferAmount)
    {
        if (totalDemand < 0) totalDemand = 0;

        double t1 = 0, t2 = 0;

        if (!t1Available && !t2Available)
            return (0, 0, 0, manualTransferEnabled
                ? "Ручной переброс недоступен: оба трансформатора отключены."
                : string.Empty);

        // Оба ручных — возвращаем как задано
        if (t1Manual && t2Manual)
        {
            t1 = t1Available ? Clamp(t1ManualPower, 0, balancerMaxT1) : 0;
            t2 = t2Available ? Clamp(t2ManualPower, 0, balancerMaxT2) : 0;
            return manualTransferEnabled
                ? (t1, t2, 0, "Ручной переброс недоступен, пока активен ручной режим трансформаторов.")
                : (t1, t2, 0, string.Empty);
        }

        // Т1 ручной, Т2 авто
        if (t1Manual && !t2Manual)
        {
            t1 = t1Available ? Clamp(t1ManualPower, 0, balancerMaxT1) : 0;
            double remaining = totalDemand - t1;
            if (remaining > 0 && t2Available)
                t2 = Clamp(remaining, 0, balancerMaxT2);
            else if (!t1Available && t2Available)
                t2 = Clamp(totalDemand, 0, balancerMaxT2);
            return manualTransferEnabled
                ? (t1, t2, 0, "Ручной переброс недоступен, пока активен ручной режим Т1.")
                : (t1, t2, 0, string.Empty);
        }

        // Т1 авто, Т2 ручной
        if (!t1Manual && t2Manual)
        {
            t2 = t2Available ? Clamp(t2ManualPower, 0, balancerMaxT2) : 0;
            double remaining = totalDemand - t2;
            if (remaining > 0 && t1Available)
                t1 = Clamp(remaining, 0, balancerMaxT1);
            else if (!t2Available && t1Available)
                t1 = Clamp(totalDemand, 0, balancerMaxT1);
            return manualTransferEnabled
                ? (t1, t2, 0, "Ручной переброс недоступен, пока активен ручной режим Т2.")
                : (t1, t2, 0, string.Empty);
        }

        // Оба авто — стратегия распределения
        (t1, t2) = DistributeAuto(totalDemand, t1Available, t2Available,
            balancerMaxT1, balancerMaxT2, t2Threshold, strategy);

        if (!manualTransferEnabled || transferAmount <= 0)
            return (t1, t2, 0, string.Empty);

        return ApplyManualTransfer(
            t1, t2,
            t1Available, t2Available,
            balancerMaxT1, balancerMaxT2,
            transferDirection, transferAmount);
    }

    private (double t1, double t2) DistributeAuto(
        double demand, bool t1Avail, bool t2Avail,
        double maxT1, double maxT2, double t2Threshold,
        DistributionStrategy strategy)
    {
        double t1 = 0, t2 = 0;

        switch (strategy)
        {
            case DistributionStrategy.T1First:
                if (t1Avail && t2Avail)
                {
                    // Т1 берёт до своего лимита, но не ниже порога подключения Т2
                    t1 = Clamp(demand, 0, maxT1);
                    // Т2 подключается только когда demand > t2Threshold
                    if (demand > t2Threshold)
                        t2 = Clamp(demand - t1, 0, maxT2);
                }
                else if (t1Avail)
                {
                    t1 = Clamp(demand, 0, maxT1);
                }
                else if (t2Avail)
                {
                    t2 = Clamp(demand, 0, maxT2);
                }
                break;

            case DistributionStrategy.T2First:
                if (t1Avail && t2Avail)
                {
                    t2 = Clamp(demand, 0, maxT2);
                    if (demand > t2Threshold)
                        t1 = Clamp(demand - t2, 0, maxT1);
                }
                else if (t2Avail)
                {
                    t2 = Clamp(demand, 0, maxT2);
                }
                else if (t1Avail)
                {
                    t1 = Clamp(demand, 0, maxT1);
                }
                break;

            case DistributionStrategy.Proportional:
                if (t1Avail && t2Avail)
                {
                    double totalCap = maxT1 + maxT2;
                    if (totalCap > 0)
                    {
                        double ratio = maxT1 / totalCap;
                        t1 = Clamp(demand * ratio, 0, maxT1);
                        t2 = Clamp(demand - t1, 0, maxT2);
                    }
                }
                else if (t1Avail)
                {
                    t1 = Clamp(demand, 0, maxT1);
                }
                else if (t2Avail)
                {
                    t2 = Clamp(demand, 0, maxT2);
                }
                break;
        }

        return (t1, t2);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static (double t1Power, double t2Power, double appliedTransfer, string warning) ApplyManualTransfer(
        double t1Power,
        double t2Power,
        bool t1Available,
        bool t2Available,
        double maxT1,
        double maxT2,
        PowerTransferDirection direction,
        double transferAmount)
    {
        if (!t1Available || !t2Available)
            return (t1Power, t2Power, 0, "Ручной переброс недоступен при отключенном трансформаторе.");

        if (direction == PowerTransferDirection.T1ToT2)
        {
            double actualTransfer = Math.Min(transferAmount, Math.Min(t1Power, maxT2 - t2Power));
            if (actualTransfer <= 0)
                return (t1Power, t2Power, 0, "Не удалось перебросить мощность из Т1 в Т2.");

            return (t1Power - actualTransfer, t2Power + actualTransfer, actualTransfer, string.Empty);
        }

        double reverseTransfer = Math.Min(transferAmount, Math.Min(t2Power, maxT1 - t1Power));
        if (reverseTransfer <= 0)
            return (t1Power, t2Power, 0, "Не удалось перебросить мощность из Т2 в Т1.");

        return (t1Power + reverseTransfer, t2Power - reverseTransfer, reverseTransfer, string.Empty);
    }
}
