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
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using System.Collections.Generic;
using QuantConnect.Data.Fundamental;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Regression algorithm adding fine fundamental data as a custom data source and making history request using it
    /// </summary>
    public class FineFundamentalDataRegressionAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Security _aaplSecurity;
        private bool _gotFineData;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2014, 6, 5);
            SetEndDate(2014, 6, 10);

            _aaplSecurity = AddEquity("AAPL", Resolution.Minute);

            var fine = AddData<FineFundamental>(_aaplSecurity.Symbol);
            var history = History<FineFundamental>(fine.Symbol, 10).ToList();
            if(history.Count == 0)
            {
                throw new Exception("Did not get any historical fine data!");
            }
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            var fine = data.Get<FineFundamental>();
            if(fine.Count > 0 && fine.Keys.Count(x => x.Underlying == _aaplSecurity.Symbol) == 1)
            {
                _gotFineData = true;
            }

            if (!Portfolio.Invested)
            {
                var cachedFine = _aaplSecurity.Cache.GetData<FineFundamental>();
                if (cachedFine != null && cachedFine.EarningRatios.EquityPerShareGrowth.OneYear > 0.01m)
                {
                    SetHoldings("AAPL", 1);
                }
            }
        }

        public override void OnEndOfAlgorithm()
        {
            if(!_gotFineData)
            {
                throw new Exception("Did not get any fine data in OnData!");
            }
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        /// <summary>
        /// Data Points count of all timeslices of algorithm
        /// </summary>
        public long DataPoints => 3160;

        /// <summary>
        /// Data Points count of the algorithm history
        /// </summary>
        public int AlgorithmHistoryDataPoints => 1;

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "1"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "76.375%"},
            {"Drawdown", "1.300%"},
            {"Expectancy", "0"},
            {"Net Profit", "0.885%"},
            {"Sharpe Ratio", "4.215"},
            {"Probabilistic Sharpe Ratio", "63.604%"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "1.177"},
            {"Beta", "-1.786"},
            {"Annual Standard Deviation", "0.135"},
            {"Annual Variance", "0.018"},
            {"Information Ratio", "1.506"},
            {"Tracking Error", "0.153"},
            {"Treynor Ratio", "-0.319"},
            {"Total Fees", "$23.93"},
            {"Estimated Strategy Capacity", "$63000000.00"},
            {"Lowest Capacity Asset", "AAPL R735QTJ8XC9X"},
            {"Portfolio Turnover", "16.81%"},
            {"OrderListHash", "0348aab328d66e6730b38551ae1d1632"}
        };
    }
}
