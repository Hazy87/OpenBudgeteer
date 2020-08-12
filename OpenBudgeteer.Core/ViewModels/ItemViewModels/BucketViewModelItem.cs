﻿using OpenBudgeteer.Core.Common;
using OpenBudgeteer.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace OpenBudgeteer.Core.ViewModels.ItemViewModels
{
    public class BucketViewModelItem : ViewModelBase
    {
        private Bucket _bucket;
        public Bucket Bucket
        {
            get => _bucket;
            set => Set(ref _bucket, value);
        }

        private BucketVersion _bucketVersion;
        public BucketVersion BucketVersion
        {
            get => _bucketVersion;
            set => Set(ref _bucketVersion, value);
        }

        private decimal _balance;
        /// <summary>
        /// Overall Balance of a <see cref="Bucket"/> for the whole time
        /// </summary>
        public decimal Balance
        {
            get => _balance;
            set => Set(ref _balance, value);
        }

        private decimal _inOut;
        /// <summary>
        /// This will be just the input field for Bucket movements
        /// </summary>
        public decimal InOut
        {
            get => _inOut;
            set => Set(ref _inOut, value);
        }

        private decimal _want;
        /// <summary>
        /// Shows how many money a <see cref="Bucket"/> want to have for a specific month
        /// </summary>
        public decimal Want
        {
            get => _want;
            set => Set(ref _want, value);
        }

        private decimal _in;
        /// <summary>
        /// Sum of all <see cref="Bucket"/> movements
        /// </summary>
        public decimal In
        {
            get => _in;
            set => Set(ref _in, value);
        }

        private decimal _activity;
        /// <summary>
        /// Sum of money for all <see cref="BankTransaction"/> in a specific month
        /// </summary>
        public decimal Activity
        {
            get => _activity;
            set => Set(ref _activity, value);
        }

        private string _details;
        /// <summary>
        /// Contains information of the progress for <see cref="Bucket"/> with <see cref="BucketVersion.BucketType"/> 3 and 4
        /// </summary>
        public string Details
        {
            get => _details;
            set => Set(ref _details, value);
        }

        private int _progress;
        /// <summary>
        /// Contains the progress in %
        /// </summary>
        public int Progress
        {
            get => _progress;
            set => Set(ref _progress, value);
        }

        private bool _isProgressBarVisible;
        /// <summary>
        /// Sets the visibility of the ProgressBar if <see cref="BucketVersion.BucketType"/> 3 or 4
        /// </summary>
        public bool IsProgressbarVisible
        {
            get => _isProgressBarVisible;
            set => Set(ref _isProgressBarVisible, value);
        }

        private bool _isHovered;
        public bool IsHovered
        {
            get => _isHovered;
            set => Set(ref _isHovered, value);
        }

        private bool _inModification;
        public bool InModification
        {
            get => _inModification;
            set => Set(ref _inModification, value);
        }

        private ObservableCollection<string> _availableBucketTypes;
        public ObservableCollection<string> AvailableBucketTypes
        {
            get => _availableBucketTypes;
            set => Set(ref _availableBucketTypes, value);
        }

        private ObservableCollection<Color> _availableColors;
        public ObservableCollection<Color> AvailableColors
        {
            get => _availableColors;
            set => Set(ref _availableColors, value);
        }

        private ObservableCollection<BucketGroup> _availableBucketGroups;
        public ObservableCollection<BucketGroup> AvailableBucketGroups
        {
            get => _availableBucketGroups;
            set => Set(ref _availableBucketGroups, value);
        }

        public event ViewModelReloadRequiredHandler ViewModelReloadRequired;
        public delegate void ViewModelReloadRequiredHandler(BucketViewModelItem sender);

        private readonly bool _isNewlyCreatedBucket;
        private readonly DateTime _currentYearMonth;
        private readonly DbContextOptions<DatabaseContext> _dbOptions;

        public BucketViewModelItem(DbContextOptions<DatabaseContext> dbOptions)
        {
            _dbOptions = dbOptions;
            AvailableBucketGroups = new ObservableCollection<BucketGroup>();
            using (var dbContext = new DatabaseContext(_dbOptions))
            {
                foreach (var item in dbContext.BucketGroup)
                {
                    AvailableBucketGroups.Add(item);
                }
            }
            AvailableBucketTypes = new ObservableCollection<string>()
            {
                "Standard Bucket",
                "Monthly expense",
                "Expense every X Months",
                "Save X until Y date"
            };
            GetKnownColors();
            InModification = false;

            void GetKnownColors()
            {
                AvailableColors = new ObservableCollection<Color>();
                var colorType = typeof(Color);
                var propInfos = colorType.GetProperties(BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public);
                foreach (var propInfo in propInfos)
                {
                    AvailableColors.Add(Color.FromName(propInfo.Name));
                }
            }
        }

        public BucketViewModelItem(DbContextOptions<DatabaseContext> dbOptions, DateTime yearMonth) : this(dbOptions)
        {
            _currentYearMonth = yearMonth;
            BucketVersion = new BucketVersion()
            {
                BucketId = 0,
                BucketType = 1,
                BucketTypeZParam = yearMonth,
                ValidFrom = yearMonth
            };
        }

        public BucketViewModelItem(DbContextOptions<DatabaseContext> dbOptions, BucketGroup bucketGroup, DateTime yearMonth) : this(dbOptions, yearMonth)
        {
            _isNewlyCreatedBucket = true;
            InModification = true;
            Bucket = new Bucket()
            {
                BucketId = 0,
                BucketGroupId = bucketGroup.BucketGroupId,
                Name = "New Bucket",
                ValidFrom = yearMonth,
                IsInactive = false,
                IsInactiveFrom = DateTime.MaxValue
            };
        }

        public BucketViewModelItem(DbContextOptions<DatabaseContext> dbOptions, Bucket bucket, DateTime yearMonth) : this(dbOptions, yearMonth)
        {
            Bucket = bucket;
            CalculateValues();
        }

        public static async Task<BucketViewModelItem> CreateAsync(DbContextOptions<DatabaseContext> dbOptions, Bucket bucket, DateTime yearMonth)
        {
            return await Task.Run(() => new BucketViewModelItem(dbOptions, bucket, yearMonth));
        }

        private void CalculateValues()
        {
            Balance = 0;
            In = 0;
            Activity = 0;
            Want = 0;
            InOut = 0;
            
            // Get latest BucketVersion based on passed parameter
            using (var bucketVersionDbContext = new DatabaseContext(_dbOptions))
            {
                var bucketVersions = bucketVersionDbContext.BucketVersion
                    .Where(i => i.BucketId == Bucket.BucketId).ToList();
                var orderedBucketVersions = bucketVersions.OrderByDescending(i => i.ValidFrom);
                foreach (var bucketVersion in orderedBucketVersions)
                {
                    if (bucketVersion.ValidFrom > _currentYearMonth) continue;
                    BucketVersion = bucketVersion;
                    break;
                }
                if (BucketVersion == null) throw new Exception("No Bucket Version found for the selected month");
            }
                        
            #region Balance

            // Get all Transactions for this Bucket until passed yearMonth
            using (var bucketTransactionsDbContext = new DatabaseContext(_dbOptions))
            {
                var bucketTransactionsSql =
                    $"SELECT a.* FROM {nameof(BudgetedTransaction)} a " +
                    $"INNER JOIN {nameof(BankTransaction)} b ON a.{nameof(BudgetedTransaction.TransactionId)} = b.{nameof(BankTransaction.TransactionId)} " +
                    $"WHERE a.{nameof(BudgetedTransaction.BucketId)} = {Bucket.BucketId} " +
                    $"AND b.{nameof(BankTransaction.TransactionDate)} < '{_currentYearMonth.AddMonths(1):yyyy-MM}-01'";
                var bucketTransactions = bucketTransactionsDbContext.BudgetedTransaction.FromSqlRaw(bucketTransactionsSql);

                foreach (BudgetedTransaction bucketTransaction in bucketTransactions)
                {
                    Balance += bucketTransaction.Amount;
                }
            }
            using (var bucketMovementsDbContext = new DatabaseContext(_dbOptions))
            {
                var bucketMovementsSql =
                    $"SELECT a.* FROM {nameof(BucketMovement)} a " +
                    $"WHERE a.{nameof(BucketMovement.BucketId)} = {Bucket.BucketId} " +
                    $"AND a.{nameof(BucketMovement.MovementDate)} < '{_currentYearMonth.AddMonths(1):yyyy-MM}-01'";
                var bucketMovements = bucketMovementsDbContext.BucketMovement.FromSqlRaw(bucketMovementsSql);

                foreach (BucketMovement bucketMovement in bucketMovements)
                {
                    Balance += bucketMovement.Amount;
                }
            }

            #endregion

            #region In & Activity

            using (var bucketTransactionsDbContext = new DatabaseContext(_dbOptions))
            {
                var bucketTransactionsCurrentMonthSql =
                    $"SELECT a.* FROM {nameof(BudgetedTransaction)} a " +
                    $"INNER JOIN {nameof(BankTransaction)} b ON a.{nameof(BudgetedTransaction.TransactionId)} = b.{nameof(BankTransaction.TransactionId)} " +
                    $"WHERE a.{nameof(BudgetedTransaction.BucketId)} = {Bucket.BucketId} " +
                    $"AND b.{nameof(BankTransaction.TransactionDate)} LIKE '{_currentYearMonth:yyyy-MM}%'";
                var bucketTransactionsCurrentMonth =
                    bucketTransactionsDbContext.BudgetedTransaction.FromSqlRaw(bucketTransactionsCurrentMonthSql);

                foreach (BudgetedTransaction bucketTransaction in bucketTransactionsCurrentMonth)
                {
                    if (bucketTransaction.Amount < 0)
                        Activity += bucketTransaction.Amount;
                    else
                        In += bucketTransaction.Amount;
                }
            }
            using (var bucketMovementsDbContext = new DatabaseContext(_dbOptions))
            {
                var bucketMovementsCurrentMonthSql =
                    $"SELECT a.* FROM {nameof(BucketMovement)} a " +
                    $"WHERE a.{nameof(BucketMovement.BucketId)} = {Bucket.BucketId} " +
                    $"AND a.{nameof(BucketMovement.MovementDate)} LIKE '{_currentYearMonth:yyyy-MM}%'";
                var bucketMovementsCurrentMonth = bucketMovementsDbContext.BucketMovement.FromSqlRaw(bucketMovementsCurrentMonthSql);

                foreach (BucketMovement bucketMovement in bucketMovementsCurrentMonth)
                {
                    if (bucketMovement.Amount < 0)
                        Activity += bucketMovement.Amount;
                    else
                        In += bucketMovement.Amount;
                }
            }            

            #endregion

            #region Want

            switch (BucketVersion.BucketType)
            {
                case 2:
                    var newWant = BucketVersion.BucketTypeYParam - In;
                    Want = newWant < 0 ? 0 : newWant;
                    break;
                case 3:
                    var nextTargetDate = BucketVersion.BucketTypeZParam;
                    while (nextTargetDate < _currentYearMonth)
                    {
                        nextTargetDate = nextTargetDate.AddMonths(BucketVersion.BucketTypeXParam);
                    }
                    Want = CalculateWant(nextTargetDate);
                    break;
                case 4:
                    Want = CalculateWant(BucketVersion.BucketTypeZParam);
                    break;
                default:
                    break;
            }

            decimal CalculateWant(DateTime targetDate)
            {
                var remainingMonths = ((targetDate.Year - _currentYearMonth.Year) * 12) + targetDate.Month - _currentYearMonth.Month;
                if (remainingMonths < 0) return Balance < 0 ? Balance : 0;
                if (remainingMonths == 0 && Balance < 0) return Balance * -1;
                var wantForThisMonth = Math.Round((BucketVersion.BucketTypeYParam - Balance + In) / (remainingMonths + 1), 2) - In;
                if (remainingMonths == 0) wantForThisMonth += Activity; // check if target amount has been consumed. Not further Want required
                return wantForThisMonth < 0 ? 0 : wantForThisMonth;
            }

            #endregion

            #region Details

            if (BucketVersion.BucketType == 3 || BucketVersion.BucketType == 4)
            {
                var targetDate = BucketVersion.BucketTypeZParam;
                // Calculate new target date for BucketType 3 (Expense every X Months) 
                // if the selected yearMonth is already in the future
                if (BucketVersion.BucketType == 3 && BucketVersion.BucketTypeZParam < _currentYearMonth)
                {
                    do
                    {
                        targetDate = targetDate.AddMonths(BucketVersion.BucketTypeXParam);
                    } while (targetDate < _currentYearMonth);
                }
                
                Progress = Convert.ToInt32((Balance / BucketVersion.BucketTypeYParam) * 100);
                Details = $"{BucketVersion.BucketTypeYParam} until {targetDate:yyyy-MM}";
                IsProgressbarVisible = true;
            }
            else
            {
                Progress = 0;
                Details = string.Empty;
                IsProgressbarVisible = false;
            }

            #endregion
        }

        public void EditBucket()
        {
            InModification = true;
        }

        public Tuple<bool, string> CloseBucket()
        {
            if (Bucket.IsInactive) return new Tuple<bool, string>(false, "Bucket has been already set to inactive");
            if (Balance != 0) return new Tuple<bool, string>(false, "Balance must be 0 to close a Bucket");
            
            using (var dbContext = new DatabaseContext(_dbOptions))
            {
                using (var transaction = dbContext.Database.BeginTransaction())
                {
                    try
                    {
                        if (dbContext.BudgetedTransaction.Any(i => i.BucketId == Bucket.BucketId) ||
                            dbContext.BucketMovement.Any(i => i.BucketId == Bucket.BucketId))
                        {
                            // Bucket will be set to inactive for the next month
                            Bucket.IsInactive = true;
                            Bucket.IsInactiveFrom = _currentYearMonth.AddMonths(1);
                            if (dbContext.UpdateBucket(Bucket) == 0) 
                                throw new Exception($"Unable to deactivate Bucket for next month.{Environment.NewLine}" +
                                                    $"{Environment.NewLine}" +
                                                    $"Bucket ID: {Bucket.BucketId}{Environment.NewLine}" +
                                                    $"Bucket Target Inactive Date: {Bucket.IsInactiveFrom.ToShortDateString()}");
                        }
                        else
                        {
                            // Bucket has no transactions & movements, so it can be directly deleted from the database
                            if (dbContext.DeleteBucket(Bucket) == 0) 
                                throw new Exception($"Unable to delete Bucket.{Environment.NewLine}" +
                                                    $"{Environment.NewLine}" +
                                                    $"Bucket ID: {Bucket.BucketId}{Environment.NewLine}");
                            var bucketVersions = dbContext.BucketVersion
                                .Where(i => i.BucketId == Bucket.BucketId)
                                .ToList();
                            foreach (var bucketVersion in bucketVersions)
                            {
                                if (dbContext.DeleteBucketVersion(bucketVersion) == 0) 
                                    throw new Exception($"Unable to delete a Bucket Version.{Environment.NewLine}" +
                                                        $"{Environment.NewLine}" +
                                                        $"Bucket Version ID: {bucketVersion.BucketVersionId}{Environment.NewLine}" +
                                                        $"Bucket Version: {bucketVersion.Version}");
                            }
                        }
                        transaction.Commit();
                    }
                    catch (Exception e)
                    {
                        transaction.Rollback();
                        return new Tuple<bool, string>(false, $"Error during database update: {e.Message}");
                    }
                }
            }            
            ViewModelReloadRequired?.Invoke(this);
            return new Tuple<bool, string>(true, string.Empty);
        }

        public Tuple<bool,string> SaveChanges()
        {
            var forceViewModelReload = false;

            if (_isNewlyCreatedBucket)
            {
                // Create new Bucket
                using (var dbContext = new DatabaseContext(_dbOptions))
                {
                    using (var transaction = dbContext.Database.BeginTransaction())
                    {
                        try
                        {
                            if (dbContext.CreateBucket(Bucket) == 0)
                                throw new Exception("Unable to create new Bucket.");

                            var newBucketVersion = BucketVersion;
                            newBucketVersion.BucketId = Bucket.BucketId;
                            newBucketVersion.Version = 1;
                            newBucketVersion.ValidFrom = _currentYearMonth;
                            if (dbContext.CreateBucketVersion(newBucketVersion) == 0) 
                                throw new Exception($"Unable to create new Bucket Version.{Environment.NewLine}" +
                                                    $"{Environment.NewLine}" +
                                                    $"Bucket ID: {newBucketVersion.BucketId}");

                            transaction.Commit();
                            ViewModelReloadRequired?.Invoke(this);
                        }
                        catch (Exception e)
                        {
                            transaction.Rollback();
                            ViewModelReloadRequired?.Invoke(this);
                            return new Tuple<bool, string>(false, $"Error during database update: {e.Message}");
                        }
                    }
                }                
            }
            else
            {
                // Check on Bucket changes and update database
                using (var dbContext = new DatabaseContext(_dbOptions))
                {
                    var dbBucket = dbContext.Bucket.First(i => i.BucketId == Bucket.BucketId);
                    if (dbBucket.Name != Bucket.Name ||
                        dbBucket.ColorCode != Bucket.ColorCode ||
                        dbBucket.BucketGroupId != Bucket.BucketGroupId)
                    {
                        // BucketGroup update requires special handling as ViewModel needs to trigger reload
                        // to force re-rendering of Blazor Page
                        if (dbBucket.BucketGroupId != Bucket.BucketGroupId) forceViewModelReload = true;

                        if (dbContext.UpdateBucket(Bucket) == 0)
                            return new Tuple<bool, string>(false, 
                                $"Error during database update: Unable to update Bucket.{Environment.NewLine}" +
                                $"{Environment.NewLine}" +
                                $"Bucket ID: {Bucket.BucketId}");
                    }
                }

                // Check on BucketVersion changes and create new BucketVersion
                using (var dbContext = new DatabaseContext(_dbOptions))
                {
                    var dbBucketVersion =
                        dbContext.BucketVersion.First(i => i.BucketVersionId == BucketVersion.BucketVersionId);
                    if (dbBucketVersion.BucketType != BucketVersion.BucketType ||
                        dbBucketVersion.BucketTypeXParam != BucketVersion.BucketTypeXParam ||
                        dbBucketVersion.BucketTypeYParam != BucketVersion.BucketTypeYParam ||
                        dbBucketVersion.BucketTypeZParam != BucketVersion.BucketTypeZParam ||
                        dbBucketVersion.Notes != BucketVersion.Notes)
                    {
                        using (var transaction = dbContext.Database.BeginTransaction())
                        {
                            try
                            {
                                if (dbContext.BucketVersion.Any(i => i.BucketId == BucketVersion.BucketId && i.Version > BucketVersion.Version))
                                    throw new Exception("Cannot create new Version as already a newer Version exists");

                                var modifiedVersion = BucketVersion;
                                if (BucketVersion.ValidFrom == _currentYearMonth)
                                {
                                    // Bucket Version modified in the same month,
                                    // so just update the version instead of creating a new version
                                    if (dbContext.UpdateBucketVersion(modifiedVersion) == 0)
                                        throw new Exception($"Unable to update Bucket Version.{Environment.NewLine}" +
                                                            $"{Environment.NewLine}" +
                                                            $"Bucket Version ID: {modifiedVersion.BucketVersionId}" +
                                                            $"Bucket ID: {modifiedVersion.BucketId}" +
                                                            $"Bucket Version: {modifiedVersion.Version}" +
                                                            $"Bucket Version Start Date: {modifiedVersion.ValidFrom.ToShortDateString()}");
                                }
                                else
                                {
                                    modifiedVersion.Version++;
                                    modifiedVersion.BucketVersionId = 0;
                                    modifiedVersion.ValidFrom = _currentYearMonth;
                                    if (dbContext.CreateBucketVersion(modifiedVersion) == 0)
                                        throw new Exception($"Unable to create new Bucket Version.{Environment.NewLine}" +
                                                            $"{Environment.NewLine}" +
                                                            $"Bucket ID: {modifiedVersion.BucketId}" +
                                                            $"Bucket Version: {modifiedVersion.Version}" +
                                                            $"Bucket Version Start Date: {modifiedVersion.ValidFrom.ToShortDateString()}");
                                }

                                transaction.Commit();
                                //ViewModelReloadRequired?.Invoke(this);
                            }
                            catch (Exception e)
                            {
                                transaction.Rollback();
                                ViewModelReloadRequired?.Invoke(this);
                                return new Tuple<bool, string>(false, $"Error during database update: {e.Message}");
                            }
                        }
                    }
                }                
            }
            InModification = false;
            CalculateValues();
            if (forceViewModelReload) ViewModelReloadRequired?.Invoke(this);
            return new Tuple<bool, string>(true, string.Empty);
        }

        public void CancelChanges()
        {
            InModification = false;
            ViewModelReloadRequired?.Invoke(this); // force Re-load to get old values back
        }

        public Tuple<bool,string> HandleInOutInput(string key)
        {
            if (key != "Enter") return new Tuple<bool, string>(true, string.Empty);
            try
            {
                using (var dbContext = new DatabaseContext(_dbOptions))
                {
                    var newMovement = new BucketMovement(Bucket, InOut, _currentYearMonth);
                    if (dbContext.CreateBucketMovement(newMovement) == 0)
                        throw new Exception($"Unable to create new Bucket Movement.{Environment.NewLine}" +
                                            $"{Environment.NewLine}" +
                                            $"Bucket ID: {newMovement.BucketId}" +
                                            $"Amount: {newMovement.Amount}" +
                                            $"Movement Date: {newMovement.MovementDate.ToShortDateString()}");
                }
                //ViewModelReloadRequired?.Invoke(this);
                CalculateValues();
            }
            catch (Exception e)
            {
                return new Tuple<bool, string>(false, $"Error during database update: {e.Message}");
            }
            return new Tuple<bool, string>(true, string.Empty);
        }
    }
}