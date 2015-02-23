﻿// ***********************************************************************
// Copyright (c) 2014 Charlie Poole
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System.IO;
using System.Globalization;
using NUnit.Common;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace NUnitLite.Runner
{
    /// <summary>
    /// ResultReporter writes the test results to a TextWriter.
    /// </summary>
    public class ResultReporter
    {
        private ExtendedTextWriter _writer;
        private ITestResult _result;
        private bool _stopOnFirstError;
        private string _overallResult;

        private int _reportIndex = 0;

        /// <summary>
        /// Constructs an instance of ResultReporter
        /// </summary>
        /// <param name="result">The top-level result being reported</param>
        /// <param name="writer">A TextWriter to which the report is written</param>
        /// <param name="stopOnFirstError">True if user requested stop after first error</param>
        public ResultReporter(ITestResult result, ExtendedTextWriter writer, bool stopOnFirstError)
        {
            _result = result;
            _writer = writer;
            _stopOnFirstError = stopOnFirstError;

            _overallResult = result.ResultState.Status.ToString();
            if (_overallResult == "Skipped")
                _overallResult = "Warning";

            Summary = new ResultSummary(_result);
        }

        /// <summary>
        /// Gets the ResultSummary created by the ResultReporter
        /// </summary>
        public ResultSummary Summary { get; private set; }

        /// <summary>
        /// Produces the standard output reports.
        /// </summary>
        public void ReportResults()
        {
            if (Summary.TestCount == 0)
                _writer.WriteLine(ColorStyle.Warning, "Warning: No tests found");

            _writer.WriteLine();

            if (_stopOnFirstError && Summary.FailureCount + Summary.ErrorCount > 0)
            {
                _writer.WriteLine(ColorStyle.Failure, "Execution terminated after first error");
                _writer.WriteLine();
            }

            WriteSummaryReport();

            if (_result.ResultState.Status == TestStatus.Failed)
                WriteErrorsAndFailuresReport();

            if (Summary.SkipCount + Summary.IgnoreCount > 0)
                WriteNotRunReport();

#if FULL
            if (commandLineOptions.Full)
                PrintFullReport(result);
#endif
        }

        #region Summmary Report

        /// <summary>
        /// Prints the Summary Report
        /// </summary>
        public void WriteSummaryReport()
        {
            var status = _result.ResultState.Status;

            ColorStyle overallStyle = status == TestStatus.Passed
                ? ColorStyle.Pass
                : status == TestStatus.Failed
                    ? ColorStyle.Failure
                    : status == TestStatus.Skipped
                        ? ColorStyle.Warning
                        : ColorStyle.Output;

            _writer.WriteLine(ColorStyle.SectionHeader, "Test Run Summary");
            _writer.WriteLabelLine("   Overall result: ", _overallResult, overallStyle);

            _writer.WriteLabel("   Tests run: ", Summary.RunCount.ToString(CultureInfo.CurrentUICulture));
            _writer.WriteLabel(", Passed: ", Summary.PassCount.ToString(CultureInfo.CurrentUICulture));
            _writer.WriteLabel(", Errors: ", Summary.ErrorCount.ToString(CultureInfo.CurrentUICulture));
            _writer.WriteLabel(", Failures: ", Summary.FailureCount.ToString(CultureInfo.CurrentUICulture));
            _writer.WriteLabelLine(", Inconclusive: ", Summary.InconclusiveCount.ToString(CultureInfo.CurrentUICulture));

            var notRunTotal = Summary.SkipCount + Summary.IgnoreCount + Summary.InvalidCount;
            _writer.WriteLabel("     Not run: ", notRunTotal.ToString(CultureInfo.CurrentUICulture));
            _writer.WriteLabel(", Invalid: ",Summary.InvalidCount.ToString(CultureInfo.CurrentUICulture));
            _writer.WriteLabel(", Ignored: ", Summary.IgnoreCount.ToString(CultureInfo.CurrentUICulture));
            _writer.WriteLabelLine(", Skipped: ", Summary.SkipCount.ToString(CultureInfo.CurrentUICulture));

            _writer.WriteLabelLine("  Start time: ", _result.StartTime.ToString("u"));
            _writer.WriteLabelLine("    End time: ", _result.EndTime.ToString("u"));
            _writer.WriteLabelLine("    Duration: ", _result.Duration.TotalSeconds.ToString("0.000") + " seconds");
            _writer.WriteLine();
        }

        #endregion

        #region Errors and Failures Report

        public void WriteErrorsAndFailuresReport()
        {
            _reportIndex = 0;
            _writer.WriteLine(ColorStyle.SectionHeader, "Errors and Failures");
            WriteErrorsAndFailures(_result);
            _writer.WriteLine();
        }

        private void WriteErrorsAndFailures(ITestResult result)
        {
            if (result.Test.IsSuite)
            {
                if (result.ResultState.Status == TestStatus.Failed)
                {
                    var suite = result.Test as TestSuite;
                    var site = result.ResultState.Site;
                    if (suite.TestType == "Theory" || site == FailureSite.SetUp || site == FailureSite.TearDown)
                        WriteSingleResult(result, ColorStyle.Failure);
                    if (site == FailureSite.SetUp) return;
                }

                foreach (ITestResult childResult in result.Children)
                    WriteErrorsAndFailures(childResult);
            }
            else if (result.ResultState.Status == TestStatus.Failed)
                WriteSingleResult(result, ColorStyle.Failure);
        }

        #endregion

        #region Not Run Report

        /// <summary>
        /// Prints the Not Run Report
        /// </summary>
        public void WriteNotRunReport()
        {
            _reportIndex = 0;
            _writer.WriteLine(ColorStyle.SectionHeader, "Tests Not Run");
            WriteNotRunResults(_result);
            _writer.WriteLine();
        }

        private void WriteNotRunResults(ITestResult result)
        {
            if (result.HasChildren)
                foreach (ITestResult childResult in result.Children)
                    WriteNotRunResults(childResult);
            else if (result.ResultState.Status == TestStatus.Skipped)
            {
                var colorStyle = result.ResultState == ResultState.Ignored
                    ? ColorStyle.Warning
                    : ColorStyle.Output;

                WriteSingleResult(result, colorStyle);
            }
        }

        #endregion

        #region Full Report

#if FULL    // Not currently used, but may be reactivated
        /// <summary>
        /// Prints a full report of all results
        /// </summary>
        public void PrintFullReport()
        {
            _writer.WriteLine();
            _writer.WriteLine(ColorStyle.SectionHeader, "All Test Results -");
            PrintAllResults(_result, " ");
        }

        private void PrintAllResults(ITestResult result, string indent)
        {
            string status = null;
            ColorStyle style = ColorStyle.Default;
            switch (result.ResultState.Status)
            {
                case TestStatus.Failed:
                    status = "FAIL";
                    style = ColorStyle.Failure;
                    break;
                case TestStatus.Skipped:
                    status = "SKIP";
                    style = ColorStyle.Warning;
                    break;
                case TestStatus.Inconclusive:
                    status = "INC ";
                    break;
                case TestStatus.Passed:
                    status = "OK  ";
                    style = ColorStyle.Pass;
                    break;
            }

            _writer.Write(style, status);
            _writer.Write(indent);
            _writer.WriteLine(status, result.Name);

            if (result.HasChildren)
                foreach (ITestResult childResult in result.Children)
                    PrintAllResults(childResult, indent + "  ");
        }
#endif

        #endregion

        #region Helper Methods

        private void PrintTestProperties(ITest test)
        {
            foreach (string key in test.Properties.Keys)
                foreach (object value in test.Properties[key])
                    _writer.WriteLabelLine(string.Format("  {0}: ", key), value);
        }

        private static readonly char[] EOL_CHARS = new char[] { '\r', '\n' };

        private void WriteSingleResult(ITestResult result, ColorStyle style)
        {
            string status = result.ResultState.Label;
            if (string.IsNullOrEmpty(status))
                status = result.ResultState.Status.ToString();

            if (status == "Failed" || status == "Error")
            {
                var site = result.ResultState.Site.ToString();
                if (site == "SetUp" || site == "TearDown")
                    status = site + " " + status;
            }

            _writer.WriteLine();
            _writer.WriteLine(
                style, string.Format("{0}) {1} : {2}", ++_reportIndex, status, result.FullName));

            if (result.Message != null && result.Message != string.Empty)
                _writer.WriteLine(style, result.Message.TrimEnd(EOL_CHARS));

            if (result.StackTrace != null && result.StackTrace != string.Empty)
                _writer.WriteLine(style, result.StackTrace.TrimEnd(EOL_CHARS));
        }

        #endregion
    }
}
