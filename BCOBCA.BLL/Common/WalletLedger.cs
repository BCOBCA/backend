using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;

namespace BCOBCA.BLL.Common;

public static class WalletLedger
{
    public static void Entry(ICommerceStore store, AffiliateWallet wallet, string type, decimal amount, decimal balance, string referenceType, Guid referenceId)
    {
        if (amount == 0) return;
        store.Set<AffiliateWalletTransaction>().Add(new AffiliateWalletTransaction { WalletId = wallet.Id, Type = type, Amount = amount, BalanceAfter = balance, ReferenceType = referenceType, ReferenceId = referenceId, CreatedAt = DateTimeOffset.UtcNow });
    }

    public static void CreditAvailable(ICommerceStore store, AffiliateWallet wallet, decimal amount, string referenceType, Guid referenceId)
    {
        wallet.AvailableBalance += amount;
        Entry(store, wallet, "CREDIT", amount, wallet.AvailableBalance, referenceType, referenceId);
        var repayment = Math.Min(wallet.AvailableBalance, wallet.DebtBalance);
        if (repayment > 0)
        {
            wallet.AvailableBalance -= repayment; wallet.DebtBalance -= repayment;
            Entry(store, wallet, "RECOVERY_DEBIT", -repayment, wallet.AvailableBalance, referenceType, referenceId);
            Entry(store, wallet, "DEBT_DEBIT", -repayment, wallet.DebtBalance, referenceType, referenceId);
        }
        wallet.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
