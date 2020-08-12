﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OpenBudgeteer.Core.Common;
using OpenBudgeteer.Core.Models;
using OpenBudgeteer.Core.ViewModels.ItemViewModels;

namespace OpenBudgeteer.Core.ViewModels
{
    public class ReportViewModel : ViewModelBase
    {
        private readonly DbContextOptions<DatabaseContext> _dbOptions;

        public ReportViewModel(DbContextOptions<DatabaseContext> dbOptions)
        {
            _dbOptions = dbOptions;
        }

        public async Task<List<Tuple<DateTime, decimal>>> LoadMonthBalancesAsync(int months = 24)
        {
            return await Task.Run(() =>
            {
                var result = new List<Tuple<DateTime, decimal>>();
                var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                using (var dbContext = new DatabaseContext(_dbOptions))
                {
                    var transactions = dbContext.BankTransaction
                        .Where(i => i.TransactionDate >= currentMonth.AddMonths(months * -1))
                        .OrderBy(i => i.TransactionDate)
                        .ToList();
                    var monthBalances = transactions
                        .GroupBy(i => new DateTime(i.TransactionDate.Year, i.TransactionDate.Month, 1))
                        .Select(i => new
                        {
                            YearMonth = i.Key,
                            Balance = i.Sum(j => j.Amount)
                        });

                    foreach (var group in monthBalances)
                    {
                        result.Add(new Tuple<DateTime, decimal>(group.YearMonth, group.Balance));
                    }
                }

                return result;
            });
        }

        public async Task<List<Tuple<DateTime, decimal, decimal>>> LoadMonthIncomeExpensesAsync(int months = 24)
        {
            return await Task.Run(() =>
            {
                var result = new List<Tuple<DateTime, decimal, decimal>>();
                var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                using (var dbContext = new DatabaseContext(_dbOptions))
                {
                    // Get all Transactions which are not marked as "Transfer"
                    var transactions = dbContext.BankTransaction
                        .Join(
                            dbContext.BudgetedTransaction,
                            bankTransaction => bankTransaction.TransactionId,
                            budgetedTransaction => budgetedTransaction.TransactionId,
                            (bankTransaction, budgetedTransaction) => new
                            {
                                bankTransaction.TransactionId,
                                bankTransaction.TransactionDate,
                                budgetedTransaction.Amount,
                                budgetedTransaction.BucketId
                            })
                        .Where(i =>
                            i.BucketId != 2 && 
                            i.TransactionDate >= currentMonth.AddMonths(months * -1))
                        .ToList();

                    var monthIncomeExpenses = transactions
                        .GroupBy(i => new DateTime(i.TransactionDate.Year, i.TransactionDate.Month, 1))
                        .Select(i => new
                        {
                            YearMonth = i.Key,
                            Income = i.Where(j => j.Amount > 0).Sum(j => j.Amount),
                            Expenses = (i.Where(j => j.Amount < 0).Sum(j => j.Amount)) * -1
                        })
                        .OrderBy(i => i.YearMonth);

                    foreach (var group in monthIncomeExpenses)
                    {
                        result.Add(new Tuple<DateTime, decimal, decimal>(group.YearMonth, group.Income, group.Expenses));
                    }
                }

                return result;
            });
        }

        public async Task<List<Tuple<DateTime, decimal, decimal>>> LoadYearIncomeExpensesAsync(int years = 5)
        {
            return await Task.Run(() =>
            {
                var result = new List<Tuple<DateTime, decimal, decimal>>();
                var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                using (var dbContext = new DatabaseContext(_dbOptions))
                {
                    // Get all Transactions which are not marked as "Transfer"
                    var transactions = dbContext.BankTransaction
                        .Join(
                            dbContext.BudgetedTransaction,
                            bankTransaction => bankTransaction.TransactionId,
                            budgetedTransaction => budgetedTransaction.TransactionId,
                            (bankTransaction, budgetedTransaction) => new
                            {
                                bankTransaction.TransactionId,
                                bankTransaction.TransactionDate,
                                budgetedTransaction.Amount,
                                budgetedTransaction.BucketId
                            })
                        .Where(i =>
                            i.BucketId != 2 &&
                            i.TransactionDate >= currentMonth.AddYears(years * -1))
                        .ToList();

                    var yearIncomeExpenses = transactions
                        .GroupBy(i => new DateTime(i.TransactionDate.Year, 1, 1))
                        .Select(i => new
                        {
                            Year = i.Key,
                            Income = i.Where(j => j.Amount > 0).Sum(j => j.Amount),
                            Expenses = (i.Where(j => j.Amount < 0).Sum(j => j.Amount)) * -1
                        })
                        .OrderBy(i => i.Year);

                    foreach (var group in yearIncomeExpenses)
                    {
                        result.Add(new Tuple<DateTime, decimal, decimal>(group.Year, group.Income, group.Expenses));
                    }
                }

                return result;
            });
        }

        public async Task<List<Tuple<DateTime, decimal>>> LoadBankBalancesAsync(int months = 24)
        {
            return await Task.Run(() =>
            {
                var result = new List<Tuple<DateTime, decimal>>();
                var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                using (var dbContext = new DatabaseContext(_dbOptions))
                {
                    for (int monthIndex = months; monthIndex >= 0; monthIndex--)
                    {
                        var month = currentMonth.AddMonths(monthIndex * -1);
                        var bankBalance = dbContext.BankTransaction
                            .Where(i => i.TransactionDate < month.AddMonths(1))
                            .OrderBy(i => i.TransactionDate)
                            .Sum(i => i.Amount);
                        result.Add(new Tuple<DateTime, decimal>(month, bankBalance));
                    }
                }

                return result;
            });
        }

        public async Task<List<MonthlyBucketExpensesReportViewModelItem>> LoadMonthExpensesBucketAsync(int month = 12)
        {
            return await Task.Run(() =>
            {
                var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month + 1, 1);
                var result = new List<MonthlyBucketExpensesReportViewModelItem>();

                using (var dbContext = new DatabaseContext(_dbOptions))
                {
                    var buckets = dbContext.Bucket
                        .Where(i => !i.IsInactive && i.BucketId > 2)
                        .OrderBy(i => i.Name);
                    foreach (var bucket in buckets)
                    {
                        // Get latest BucketVersion based on passed parameter
                        using (var bucketVersionDbContext = new DatabaseContext(_dbOptions))
                        {
                            var newReportRecord = new MonthlyBucketExpensesReportViewModelItem();
                            var latestVersion = bucketVersionDbContext.BucketVersion
                                .Where(i => i.BucketId == bucket.BucketId)
                                .ToList()
                                .OrderByDescending(i => i.Version)
                                .First();
                            if (latestVersion.BucketType != 2) continue;
                            using (var budgetedTransactionDbContext = new DatabaseContext(_dbOptions))
                            {
                                var queryResults = budgetedTransactionDbContext.BankTransaction
                                    // Join with BudgetedTransaction
                                    .Join(budgetedTransactionDbContext.BudgetedTransaction,
                                        transaction => transaction.TransactionId,
                                        budgetedTransaction => budgetedTransaction.TransactionId,
                                        ((transaction, budgetedTransaction) => new
                                        {
                                            Transaction = transaction,
                                            BudgetedTransaction = budgetedTransaction
                                        }))
                                    // Limit on Transactions for the current Bucket and the last x months
                                    .Where(i => i.BudgetedTransaction.BucketId == bucket.BucketId &&
                                            i.Transaction.TransactionDate >= currentMonth.AddMonths(month * -1))
                                    // Group the results per YearMonth
                                    .GroupBy(i => new DateTime(i.Transaction.TransactionDate.Year, i.Transaction.TransactionDate.Month, 1))
                                    // Create a new Grouped Object
                                    .Select(i => new
                                    {
                                        YearMonth = i.Key,
                                        Balance = (i.Sum(j => j.Transaction.Amount)) * -1
                                    })
                                    .ToList();

                                // Collect results
                                newReportRecord.BucketName = bucket.Name;
                                foreach (var queryResult in queryResults)
                                {
                                    newReportRecord.MonthlyResults.Add(new Tuple<DateTime, decimal>(
                                        queryResult.YearMonth,
                                        queryResult.Balance));    
                                }
                            }
                            result.Add(newReportRecord);
                        }
                    }
                }
                return result;
            });
        }
    }
}
