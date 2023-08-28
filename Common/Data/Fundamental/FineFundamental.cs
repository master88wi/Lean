/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Concurrent;
using static QuantConnect.StringExtensions;

namespace QuantConnect.Data.Fundamental
{
    /// <summary>
    /// Definition of the FineFundamental class
    /// </summary>
    public partial class FineFundamental
    {
        private static readonly ConcurrentDictionary<int, List<DateTime>> FineFilesCache
            = new ConcurrentDictionary<int, List<DateTime>>();

        /// <summary>
        /// The end time of this data.
        /// </summary>
        [JsonIgnore]
        public override DateTime EndTime
        {
            get { return Time + QuantConnect.Time.OneDay; }
            set { Time = value - QuantConnect.Time.OneDay; }
        }

        /// <summary>
        /// Price * Total SharesOutstanding.
        /// The most current market cap for example, would be the most recent closing price x the most recent reported shares outstanding.
        /// For ADR share classes, market cap is price * (ordinary shares outstanding / adr ratio).
        /// </summary>
        [JsonIgnore]
        public long MarketCap => CompanyProfile?.MarketCap ?? 0;


        /// <summary>
        /// Creates the universe symbol used for fine fundamental data
        /// </summary>
        /// <param name="market">The market</param>
        /// <param name="addGuid">True, will add a random GUID to allow uniqueness</param>
        /// <returns>A fine universe symbol for the specified market</returns>
        public static Symbol CreateUniverseSymbol(string market, bool addGuid = true)
        {
            market = market.ToLowerInvariant();
            var ticker = $"qc-universe-fine-{market}";
            if (addGuid)
            {
                ticker += $"-{Guid.NewGuid()}";
            }
            var sid = SecurityIdentifier.GenerateEquity(SecurityIdentifier.DefaultDate, ticker, market);
            return new Symbol(sid, ticker);
        }

        /// <summary>
        /// Return the URL string source of the file. This will be converted to a stream
        /// </summary>
        public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            return GetSource(this, config, date, isLiveMode);
        }

        /// <summary>
        /// Reader converts each line of the data source into BaseData objects. Each data type creates its own factory method, and returns a new instance of the object
        /// each time it is called. The returned object is assumed to be time stamped in the config.ExchangeTimeZone.
        /// </summary>
        public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            var data = JsonConvert.DeserializeObject<FineFundamental>(line);

            data.DataType = MarketDataType.Auxiliary;
            data.Symbol = config.Symbol;
            data.Time = date;

            return data;
        }

        /// <summary>
        /// Clones this fine data instance
        /// </summary>
        /// <returns></returns>
        public override BaseData Clone()
        {
            return new FineFundamental
            {
                DataType = MarketDataType.Auxiliary,
                Symbol = Symbol,
                Time = Time,
                CompanyReference = CompanyReference,
                SecurityReference = SecurityReference,
                FinancialStatements = FinancialStatements,
                EarningReports = EarningReports,
                OperationRatios = OperationRatios,
                EarningRatios = EarningRatios,
                ValuationRatios = ValuationRatios,
                AssetClassification = AssetClassification,
                CompanyProfile = CompanyProfile
            };
        }

        /// <summary>
        /// This is a daily data set
        /// </summary>
        public override List<Resolution> SupportedResolutions()
        {
            return DailyResolution;
        }

        /// <summary>
        /// This is a daily data set
        /// </summary>
        public override Resolution DefaultResolution()
        {
            return Resolution.Daily;
        }

        private static SubscriptionDataSource GetSourceForDatae(FineFundamental fine, SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            var basePath = Globals.GetDataFolderPath(Invariant($"equity/{config.Market}/fundamental/fine"));
            var source = Path.Combine(basePath, Invariant($"{config.Symbol.Value.ToLowerInvariant()}/{date:yyyyMMdd}.zip"));

            return new SubscriptionDataSource(source, SubscriptionTransportMedium.LocalFile, FileFormat.Csv);
        }

        /// <summary>
        /// Returns a SubscriptionDataSource for the FineFundamental class,
        /// returning data from a previous date if not available for the requested date
        /// </summary>
        private static SubscriptionDataSource GetSource(FineFundamental fine, SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            var source = GetSourceForDatae(fine, config, date, isLiveMode);

            if (File.Exists(source.Source))
            {
                return source;
            }

            if (isLiveMode)
            {
                var result = DailyBackwardsLoop(fine, config, date, source, isLiveMode);
                // if we didn't fine any file we just fallback into listing the directory
                if (result != null)
                {
                    return result;
                }
            }

            var cacheKey = config.Symbol.Value.ToLowerInvariant().GetHashCode();
            List<DateTime> availableDates;

            // only use cache in backtest, since in live mode new fine files are added
            // we still didn't load available fine dates for this symbol
            if (isLiveMode || !FineFilesCache.TryGetValue(cacheKey, out availableDates))
            {
                try
                {
                    var path = Path.GetDirectoryName(source.Source) ?? string.Empty;
                    availableDates = Directory.GetFiles(path, "*.zip")
                        .Select(
                            filePath =>
                            {
                                try
                                {
                                    return DateTime.ParseExact(
                                        Path.GetFileNameWithoutExtension(filePath),
                                        "yyyyMMdd",
                                        CultureInfo.InvariantCulture
                                    );
                                }
                                catch
                                {
                                    // just in case...
                                    return DateTime.MaxValue;
                                }
                            }
                        )
                        .Where(time => time != DateTime.MaxValue)
                        .OrderBy(x => x)
                        .ToList();
                }
                catch
                {
                    // directory doesn't exist or path is null
                    if (!isLiveMode)
                    {
                        // only add to cache if not live mode
                        FineFilesCache[cacheKey] = new List<DateTime>();
                    }
                    return source;
                }

                if (!isLiveMode)
                {
                    // only add to cache if not live mode
                    FineFilesCache[cacheKey] = availableDates;
                }
            }

            // requested date before first date, return null source
            if (availableDates.Count == 0 || date < availableDates[0])
            {
                return source;
            }
            for (var i = availableDates.Count - 1; i >= 0; i--)
            {
                // we iterate backwards ^ and find the first data point before 'date'
                if (availableDates[i] <= date)
                {
                    return GetSourceForDatae(fine, config, availableDates[i], isLiveMode);
                }
            }

            return source;
        }

        private static SubscriptionDataSource DailyBackwardsLoop(FineFundamental fine, SubscriptionDataConfig config, DateTime date, SubscriptionDataSource source, bool isLiveMode)
        {
            var path = Path.GetDirectoryName(source.Source) ?? string.Empty;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                // directory does not exist
                return source;
            }

            // loop back in time, for 10 days, until we find an existing file
            var count = 10;
            do
            {
                // get previous date
                date = date.AddDays(-1);

                // get file name for this date
                source = GetSourceForDatae(fine, config, date, isLiveMode);
                if (File.Exists(source.Source))
                {
                    break;
                }
            }
            while (--count > 0);

            return count == 0 ? null : source;
        }
    }
}
