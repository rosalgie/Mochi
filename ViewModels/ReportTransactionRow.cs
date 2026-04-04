using System;
using System.Text;
using Mochi.Domain;

namespace Mochi.ViewModels;

public class ReportTransactionRow
{
    public ReportTransactionRow(Transaction transaction)
    {
        TimestampLocal = transaction.TimestampUtc.ToLocalTime();
        TypeLabel = transaction.IsExpense ? "Expense" : "Income";
        CategoryLabel = FormatCategoryLabel(transaction.Category);
        Description = transaction.Description;
        Amount = transaction.Amount;
        IsExpense = transaction.IsExpense;
    }

    public DateTime TimestampLocal { get; }
    public string TypeLabel { get; }
    public string CategoryLabel { get; }
    public string Description { get; }
    public int Amount { get; }
    public bool IsExpense { get; }
    public int SignedAmount => IsExpense ? -Amount : Amount;
    public string AmountDisplay => $"{(IsExpense ? "-" : "+")}${Amount}";

    public static string FormatCategoryLabel(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return "Uncategorized";

        StringBuilder builder = new();
        for (int i = 0; i < category.Length; i++)
        {
            char current = category[i];
            if (i > 0 &&
                char.IsUpper(current) &&
                (char.IsLower(category[i - 1]) ||
                 (i + 1 < category.Length && char.IsLower(category[i + 1]))))
                builder.Append(' ');

            builder.Append(current);
        }

        return builder.ToString();
    }
}
