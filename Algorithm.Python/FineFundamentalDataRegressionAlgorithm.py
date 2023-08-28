# QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
# Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

from AlgorithmImports import *

### <summary>
### Regression algorithm adding fine fundamental data as a custom data source and making history request using it
### </summary>
class FineFundamentalDataRegressionAlgorithm(QCAlgorithm):
    '''Regression algorithm adding fine fundamental data as a custom data source and making history request using it'''

    def Initialize(self):
        '''Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.'''

        self.SetStartDate(2014, 6, 5)
        self.SetEndDate(2014, 6, 10)

        self._aaplSecurity = self.AddEquity("AAPL", Resolution.Minute)

        fine = self.AddData(FineFundamental, self._aaplSecurity.Symbol)
        history = self.History(FineFundamental, fine.Symbol, 10)
        if len(history) == 0:
            raise ValueError("Did not get any historical fine data!")

    def OnData(self, data):
        '''OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.

        Arguments:
            data: Slice object keyed by symbol containing the stock data
        '''
        fine = data.Get(FineFundamental);
        if fine.Count > 0 and self._aaplSecurity.Symbol in [ fineSymbol.Underlying for fineSymbol in fine.Keys ]:
            self._gotFineData = True;

        if not self.Portfolio.Invested:
            cachedFine = self._aaplSecurity.Cache.GetData(FineFundamental)
            if cachedFine and cachedFine.EarningRatios.EquityPerShareGrowth.OneYear > 0.01:
                self.SetHoldings("AAPL", 1)

    def OnEndOfAlgorithm(self):
        if not self._gotFineData:
            raise ValueError("Did not get any fine data in OnData!");
