using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mochi.Domain;
using Mochi.Services;

namespace Mochi.ViewModels;

public partial class ReportViewModel : ViewModelBase
{
    private const string AllTransactionsLabel = "All";
    private const string IncomeOnlyLabel = "Income";
    private const string ExpensesOnlyLabel = "Expenses";
    private const string AllCategoriesLabel = "All categories";
    private const string NewestFirstLabel = "Newest to oldest";
    private const string OldestFirstLabel = "Oldest to newest";
    private const string HighestAmountLabel = "Amount high to low";
    private const string LowestAmountLabel = "Amount low to high";

    private readonly SaveData _save;

    [ObservableProperty] private IReadOnlyList<HistoryEntry> _history = [];
    [ObservableProperty] private IReadOnlyList<CategorySummary> _selectionBreakdown = [];
    [ObservableProperty] private IReadOnlyList<CategorySummary> _spendingByCategory = [];
    [ObservableProperty] private IReadOnlyList<string> _categoryOptions = [AllCategoriesLabel];
    [ObservableProperty] private IReadOnlyList<ReportTransactionRow> _filteredTransactions = [];
    [ObservableProperty] private string _selectedCategory = AllCategoriesLabel;
    [ObservableProperty] private string _selectedSort = NewestFirstLabel;
    [ObservableProperty] private string _selectedTransactionType = AllTransactionsLabel;
    [ObservableProperty] private string _exportStatusMessage = "Export the current filtered report to CSV.";

    public ReportViewModel(AppConfig config, SaveData save)
    {
        _save = save;
        PetName = config.PetName;
        Difficulty = config.Difficulty;
        Personality = config.Personality;
        CreatedDate = config.CreatedUtc.ToLocalTime().ToString("MMMM d, yyyy");
        Refresh();
    }

    /// <summary>Design-time constructor.</summary>
    public ReportViewModel() : this(new AppConfig { PetName = "Designer" }, SaveData.CreateDefault())
    {
    }

    public string PetName { get; }
    public Difficulty Difficulty { get; }
    public Personality Personality { get; }
    public string CreatedDate { get; }
    public IReadOnlyList<string> TransactionTypeOptions { get; } = [AllTransactionsLabel, IncomeOnlyLabel, ExpensesOnlyLabel];
    public IReadOnlyList<string> SortOptions { get; } = [NewestFirstLabel, OldestFirstLabel, HighestAmountLabel, LowestAmountLabel];

    public int TotalEarned => _save.Transactions
        .Where(t => !t.IsExpense).Sum(t => t.Amount);

    public int TotalSpent => _save.Transactions
        .Where(t => t.IsExpense).Sum(t => t.Amount);

    public int CurrentBalance => _save.WalletBalance;

    public bool HasPurchases => _save.Transactions.Any(t => t.IsExpense);
    public bool HasSelectionBreakdown => SelectionBreakdown.Count > 0;
    public bool HasFilteredTransactions => FilteredTransactions.Count > 0;
    public bool HasNoFilteredTransactions => !HasFilteredTransactions;
    public int FilteredTransactionCount => FilteredTransactions.Count;
    public int FilteredIncomeTotal => FilteredTransactions.Where(t => !t.IsExpense).Sum(t => t.Amount);
    public int FilteredExpenseTotal => FilteredTransactions.Where(t => t.IsExpense).Sum(t => t.Amount);
    public int FilteredNetChange => FilteredIncomeTotal - FilteredExpenseTotal;
    public string FilteredNetChangeDisplay => $"{(FilteredNetChange >= 0 ? "+" : "-")}${Math.Abs(FilteredNetChange)}";
    public string SelectedCategoryLabel => SelectedCategory == AllCategoriesLabel
        ? SelectedCategory
        : ReportTransactionRow.FormatCategoryLabel(SelectedCategory);

    public string SelectedViewDescription =>
        $"{SelectedTransactionType} | {SelectedCategoryLabel} | {SelectedSort}";

    public string FilteredEmptyStateMessage
    {
        get
        {
            string transactionLabel = SelectedTransactionType == AllTransactionsLabel
                ? "transactions"
                : $"{SelectedTransactionType.ToLowerInvariant()} transactions";

            return SelectedCategory == AllCategoriesLabel
                ? $"No {transactionLabel} match the current filters yet."
                : $"No {transactionLabel} match {SelectedCategoryLabel} yet.";
        }
    }

    public string SelectionInsight
    {
        get
        {
            if (!HasFilteredTransactions) return "Adjust the filters, then export the custom view to CSV.";

            if (SelectionBreakdown.Count == 0)
                return $"Showing {FilteredTransactionCount} matching transactions.";

            if (SelectionBreakdown.Count == 1)
                return $"{SelectionBreakdown[0].Category} is the only category in this view at ${SelectionBreakdown[0].Total}.";

            CategorySummary largestCategory = SelectionBreakdown[0];
            return $"{largestCategory.Category} is the largest category in this view at ${largestCategory.Total}.";
        }
    }

    partial void OnSelectedTransactionTypeChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedViewDescription));
        OnPropertyChanged(nameof(FilteredEmptyStateMessage));
        UpdateCategoryOptions();
        ApplyFilters();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedCategoryLabel));
        OnPropertyChanged(nameof(SelectedViewDescription));
        OnPropertyChanged(nameof(FilteredEmptyStateMessage));
        ApplyFilters();
    }

    partial void OnSelectedSortChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedViewDescription));
        ApplyFilters();
    }

    [RelayCommand]
    private async Task ExportCsv()
    {
        try
        {
            AppPaths.EnsureReportsFolderExists();

            string safePetName = BuildSafeFileName(PetName);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string filePath = Path.Combine(AppPaths.ReportsPath, $"{safePetName}-report-{timestamp}.csv");

            await File.WriteAllTextAsync(filePath, BuildCsv(), Encoding.UTF8);
            ExportStatusMessage = $"CSV saved to {filePath}";
        }
        catch (Exception ex)
        {
            ExportStatusMessage = $"CSV export failed: {ex.Message}";
        }
    }

    private void UpdateCategoryOptions()
    {
        List<string> categoryOptions =
        [
            AllCategoriesLabel,
            ..GetTransactionsForType()
                .Select(t => t.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
        ];

        CategoryOptions = categoryOptions;
        if (!categoryOptions.Contains(SelectedCategory)) SelectedCategory = AllCategoriesLabel;
    }

    private void ApplyFilters()
    {
        IEnumerable<Transaction> filteredQuery = GetTransactionsForType();
        if (SelectedCategory != AllCategoriesLabel)
            filteredQuery = filteredQuery.Where(t => string.Equals(t.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));

        List<Transaction> filteredTransactions = SortTransactions(filteredQuery).ToList();

        FilteredTransactions = filteredTransactions
            .Select(t => new ReportTransactionRow(t))
            .ToList();
        SelectionBreakdown = ComputeCategorySummary(filteredTransactions);

        OnPropertyChanged(nameof(HasSelectionBreakdown));
        OnPropertyChanged(nameof(HasFilteredTransactions));
        OnPropertyChanged(nameof(HasNoFilteredTransactions));
        OnPropertyChanged(nameof(FilteredTransactionCount));
        OnPropertyChanged(nameof(FilteredIncomeTotal));
        OnPropertyChanged(nameof(FilteredExpenseTotal));
        OnPropertyChanged(nameof(FilteredNetChange));
        OnPropertyChanged(nameof(FilteredNetChangeDisplay));
        OnPropertyChanged(nameof(SelectionInsight));
    }

    private IEnumerable<Transaction> GetTransactionsForType()
    {
        return SelectedTransactionType switch
        {
            IncomeOnlyLabel => _save.Transactions.Where(t => !t.IsExpense),
            ExpensesOnlyLabel => _save.Transactions.Where(t => t.IsExpense),
            _ => _save.Transactions
        };
    }

    private IEnumerable<Transaction> SortTransactions(IEnumerable<Transaction> transactions)
    {
        return SelectedSort switch
        {
            OldestFirstLabel => transactions
                .OrderBy(t => t.TimestampUtc)
                .ThenByDescending(t => t.Amount),
            HighestAmountLabel => transactions
                .OrderByDescending(t => t.Amount)
                .ThenByDescending(t => t.TimestampUtc),
            LowestAmountLabel => transactions
                .OrderBy(t => t.Amount)
                .ThenByDescending(t => t.TimestampUtc),
            _ => transactions
                .OrderByDescending(t => t.TimestampUtc)
                .ThenByDescending(t => t.Amount)
        };
    }

    private IReadOnlyList<CategorySummary> ComputeCategorySummary(IEnumerable<Transaction> transactions)
    {
        return transactions
            .GroupBy(t => t.Category)
            .Select(g => new CategorySummary(
                ReportTransactionRow.FormatCategoryLabel(g.Key),
                g.Sum(t => t.Amount)))
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Category)
            .ToList();
    }

    public void Refresh()
    {
        History = _save.History.AsEnumerable().Reverse().ToList();
        SpendingByCategory = ComputeCategorySummary(_save.Transactions.Where(t => t.IsExpense));
        UpdateCategoryOptions();
        ApplyFilters();
        OnPropertyChanged(nameof(TotalEarned));
        OnPropertyChanged(nameof(TotalSpent));
        OnPropertyChanged(nameof(CurrentBalance));
        OnPropertyChanged(nameof(HasPurchases));
    }

    private string BuildCsv()
    {
        StringBuilder builder = new();
        builder.AppendLine(
            "Pet Name,Difficulty,Personality,Created,Filter Type,Category Filter,Sort,Transaction Count,Income Total,Expense Total,Net Change,Timestamp,Transaction Type,Category,Description,Amount");

        if (!HasFilteredTransactions)
        {
            builder.AppendLine(string.Join(",",
                EscapeCsv(PetName),
                EscapeCsv(Difficulty.ToString()),
                EscapeCsv(Personality.ToString()),
                EscapeCsv(CreatedDate),
                EscapeCsv(SelectedTransactionType),
                EscapeCsv(SelectedCategoryLabel),
                EscapeCsv(SelectedSort),
                FilteredTransactionCount.ToString(),
                FilteredIncomeTotal.ToString(),
                FilteredExpenseTotal.ToString(),
                FilteredNetChange.ToString(),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty));
            return builder.ToString();
        }

        foreach (ReportTransactionRow transaction in FilteredTransactions)
            builder.AppendLine(string.Join(",",
                EscapeCsv(PetName),
                EscapeCsv(Difficulty.ToString()),
                EscapeCsv(Personality.ToString()),
                EscapeCsv(CreatedDate),
                EscapeCsv(SelectedTransactionType),
                EscapeCsv(SelectedCategoryLabel),
                EscapeCsv(SelectedSort),
                FilteredTransactionCount.ToString(),
                FilteredIncomeTotal.ToString(),
                FilteredExpenseTotal.ToString(),
                FilteredNetChange.ToString(),
                EscapeCsv(transaction.TimestampLocal.ToString("g")),
                EscapeCsv(transaction.TypeLabel),
                EscapeCsv(transaction.CategoryLabel),
                EscapeCsv(transaction.Description),
                transaction.SignedAmount.ToString()));

        return builder.ToString();
    }

    private static string BuildSafeFileName(string name)
    {
        IEnumerable<char> safeCharacters = name
            .Where(c => !Path.GetInvalidFileNameChars().Contains(c))
            .Select(c => char.IsWhiteSpace(c) ? '-' : c);
        string safeName = new string(safeCharacters.ToArray()).Trim('-');
        return string.IsNullOrWhiteSpace(safeName) ? "mochi" : safeName.ToLowerInvariant();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
