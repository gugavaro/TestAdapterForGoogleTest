﻿using GoogleTestAdapterUiTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using TestStack.White;
using TestStack.White.Configuration;
using TestStack.White.Factory;
using TestStack.White.UIItems;
using TestStack.White.UIItems.Finders;
using TestStack.White.UIItems.WindowItems;
using TestStack.White.UIItems.WPFUIItems;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using System.Collections.Generic;

namespace GoogleTestAdapterUiTests
{
    [TestClass]
    public class UiTests
    {
        private const string BatchTeardownWarning = "Warning: Test teardown batch returned exit code 1";

        private const bool keepDirtyInstanceInit = false;
        private const bool overwriteTestResults = false;
        private static readonly int TimeOut = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;


        static UiTests()
        {
            string testDll = Assembly.GetExecutingAssembly().Location;
            Match match = Regex.Match(testDll, @"^(.*)\\GoogleTestExtension\\GoogleTestAdapterUiTests\\bin\\(Debug|Release)\\GoogleTestAdapterUiTests.dll$");
            Assert.IsTrue(match.Success);

            string basePath = match.Groups[1].Value;
            string debugOrRelease = match.Groups[2].Value;
            vsixPath = Path.Combine(basePath, @"GoogleTestExtension\GoogleTestAdapterVSIX\bin", debugOrRelease, @"GoogleTestAdapterVSIX.vsix");
            solution = Path.Combine(basePath, @"SampleGoogleTestTests\SampleGoogleTestTests.sln");
            uiTestsPath = Path.Combine(basePath, @"GoogleTestExtension\GoogleTestAdapterUiTests");
            userSettings = Path.Combine(basePath, @"SampleGoogleTestTests\NonDeterministic.runsettings");
            noSettings = Path.Combine(basePath, @"SampleGoogleTestTests\No.runsettings");
        }

        private static readonly string vsixPath;
        private static readonly string solution;
        private static readonly string uiTestsPath;
        private static readonly string userSettings;
        private static readonly string noSettings;
        private static bool keepDirtyVsInstance = keepDirtyInstanceInit;

        private static VsExperimentalInstance visualStudioInstance;
        private static Application application;
        private static Window mainWindow;
        private static IUIItem testExplorer;

        [ClassInitialize]
        public static void SetupVanillaVsExperimentalInstance(TestContext testContext)
        {
            string solutionDir = Path.GetDirectoryName(solution);
            string vsDir = Path.Combine(solutionDir, ".vs");
            if (Directory.Exists(vsDir))
            {
                Directory.Delete(vsDir, true);
            }

            try
            {
                visualStudioInstance = new VsExperimentalInstance(VsExperimentalInstance.Versions.VS2015, "GoogleTestAdapterUiTests");
                if (!keepDirtyVsInstance)
                {
                    keepDirtyVsInstance = AskToCleanIfExists(visualStudioInstance);
                }
                if (!keepDirtyVsInstance)
                {
                    visualStudioInstance.FirstTimeInitialization();
                    visualStudioInstance.InstallExtension(vsixPath);
                }
            }
            catch (AutomationException exception)
            {
                LogExceptionAndThrow(exception);
            }

            application = visualStudioInstance.Launch();
            CoreAppXmlConfiguration.Instance.ApplyTemporarySetting(
                c => { c.BusyTimeout = c.FindWindowTimeout = TimeOut; });
            mainWindow = application.GetWindow(
                SearchCriteria.ByAutomationId("VisualStudioMainWindow"),
                InitializeOption.NoCache);
        }

        [TestInitialize]
        public void OpenSolutionAndTestExplorer()
        {
            OpenSolution();

            if (testExplorer == null)
            {
                testExplorer = OpenTestExplorer();
                WaitForTestDiscovery();
            }
            else
            {
                testExplorer = OpenTestExplorer();
            }
        }

        [TestCleanup]
        public void CloseSolution()
        {
            mainWindow.VsMenuBarMenuItems("File", "Close Solution").Click();
        }

        [ClassCleanup]
        public static void CleanVsExperimentalInstance()
        {
            mainWindow.Dispose();
            application.Dispose();

            mainWindow = null;
            application = null;
            testExplorer = null;

            if (!keepDirtyVsInstance)
            {
                // wait for removal of locks on some files we want to delete
                // TODO: find more reliable method than using Sleep()
                Thread.Sleep(TimeSpan.FromSeconds(1));
                visualStudioInstance.Clean();
            }
            visualStudioInstance = null;
        }

        [TestMethod]
        [TestCategory("UI")]
        public void RunAllTests__AllTestsAreRun()
        {
            try
            {
                // Run all tests and wait till finish (max 1 minute)
                mainWindow.VsMenuBarMenuItems("Test", "Run", "All Tests").Click();
                ProgressBar progressIndicator = testExplorer.Get<ProgressBar>("runProgressBar");
                mainWindow.WaitTill(() => progressIndicator.Value == progressIndicator.Maximum, TimeSpan.FromMinutes(1));

                CheckResults();
            }
            catch (AutomationException exception)
            {
                LogExceptionAndThrow(exception);
            }
        }

        [TestMethod]
        [TestCategory("UI")]
        public void RunSelectedTests_Crashing_AddPasses()
        {
            ExecuteSingleTestCase("Crashing.AddPasses");
        }

        [TestMethod]
        [TestCategory("UI")]
        public void RunSelectedTests_ParameterizedTests_Simple_0()
        {
            ExecuteSingleTestCase("ParameterizedTests.Simple/0");
        }

        [TestMethod]
        [TestCategory("UI")]
        public void RunSelectedTests_InstantiationName_ParameterizedTests_SimpleTraits_0()
        {
            ExecuteSingleTestCase("InstantiationName/ParameterizedTests.SimpleTraits/0");
        }

        [TestMethod]
        [TestCategory("UI")]
        public void RunSelectedTests_PointerParameterizedTests_CheckStringLength_0()
        {
            ExecuteSingleTestCase("PointerParameterizedTests.CheckStringLength/0");
        }

        [TestMethod]
        [TestCategory("UI")]
        public void RunSelectedTests_TypedTests_0_CanIterate()
        {
            ExecuteSingleTestCase("TypedTests/0.CanIterate");
        }

        [TestMethod]
        [TestCategory("UI")]
        public void RunSelectedTests_Arr_TypeParameterizedTests_1_CanDefeatMath()
        {
            ExecuteSingleTestCase("Arr/TypeParameterizedTests/1.CanDefeatMath");
        }

        [TestMethod]
        [TestCategory("UI")]
        public void RunSelectedTests_MultipleTests()
        {
            ExecuteMultipleTestCases(new[] { "Crashing.AddPasses", "ParameterizedTests.Simple/0",
                "InstantiationName/ParameterizedTests.SimpleTraits/0", "PointerParameterizedTests.CheckStringLength/0",
                "TypedTests/0.CanIterate", "Arr/TypeParameterizedTests/1.CanDefeatMath" });
        }

        [TestMethod]
        [TestCategory("UI")]
        public void RunAllTests_GlobalAndSolutionSettings_BatchTeardownWarning()
        {
            try
            {
                mainWindow.VsMenuBarMenuItems("Test", "Run", "All Tests").Click();
                ProgressBar progressIndicator = testExplorer.Get<ProgressBar>("runProgressBar");
                mainWindow.WaitTill(() => progressIndicator.Value == progressIndicator.Maximum, TimeSpan.FromMinutes(1));

                IUIItem outputWindow = mainWindow.Get(SearchCriteria.ByText("Output").AndByClassName("GenericPane"), TimeSpan.FromSeconds(10));
                string output = outputWindow.Get<TextBox>("WpfTextView").Text;
                Assert.IsTrue(output.Contains(BatchTeardownWarning));
            }
            catch (AutomationException exception)
            {
                LogExceptionAndThrow(exception);
            }
        }

        [TestMethod]
        [TestCategory("UI")]
        public void RunAllTests_UserSettings_ShuffledTestExecutionAndNoBatchWarning()
        {
            try
            {
                try
                {
                    SelectTestSettingsFile(userSettings);

                    mainWindow.VsMenuBarMenuItems("Test", "Run", "All Tests").Click();
                    ProgressBar progressIndicator = testExplorer.Get<ProgressBar>("runProgressBar");
                    mainWindow.WaitTill(() => progressIndicator.Value == progressIndicator.Maximum, TimeSpan.FromMinutes(1));

                    IUIItem outputWindow = mainWindow.Get(SearchCriteria.ByText("Output").AndByClassName("GenericPane"), TimeSpan.FromSeconds(10));
                    string output = outputWindow.Get<TextBox>("WpfTextView").Text;
                    Assert.IsTrue(output.Contains("--gtest_shuffle"));
                    Assert.IsTrue(output.Contains("--gtest_repeat=5"));
                    Assert.IsFalse(output.Contains(BatchTeardownWarning));
                }
                finally
                {
                    UnselectTestSettingsFile();
                }
            }
            catch (AutomationException exception)
            {
                LogExceptionAndThrow(exception);
            }
        }


        private void ExecuteSingleTestCase(string displayName, [CallerMemberName] string testCaseName = null)
        {
            ExecuteMultipleTestCases(new[] { displayName }, testCaseName);
        }

        private void ExecuteMultipleTestCases(string[] displayNames, [CallerMemberName] string testCaseName = null)
        {
            try
            {
                // Run a selected test and wait till finish (max 1 minute)
                TestExplorerUtil util = new TestExplorerUtil(testExplorer);
                util.SelectTestCases(displayNames);
                mainWindow.VsMenuBarMenuItems("Test", "Run", "Selected Tests").Click();
                ProgressBar progressIndicator = testExplorer.Get<ProgressBar>("runProgressBar");
                mainWindow.WaitTill(
                    () => progressIndicator.Value == progressIndicator.Maximum, TimeSpan.FromSeconds(10));

                CheckResults(testCaseName);
            }
            catch (AutomationException exception)
            {
                LogExceptionAndThrow(exception);
            }
        }

        private void OpenSolution()
        {
            mainWindow.VsMenuBarMenuItems("File", "Open", "Project/Solution...").Click();
            FillFileDialog("Open Project", solution);
        }

        private void SelectTestSettingsFile(string settingsFile)
        {
            mainWindow.VsMenuBarMenuItems("Test", "Test Settings", "Select Test Settings File").Click();
            FillFileDialog("Open Settings File", settingsFile);
        }

        private void UnselectTestSettingsFile()
        {
            SelectTestSettingsFile(noSettings);
        }

        private IUIItem OpenTestExplorer()
        {
            mainWindow.VsMenuBarMenuItems("Test", "Windows", "Test Explorer").Click();
            return mainWindow.Get<UIItem>("TestWindowToolWindowControl");
        }

        private void WaitForTestDiscovery()
        {
            ProgressBar delayIndicator = testExplorer.Get<ProgressBar>("delayIndicatorProgressBar");
            mainWindow.WaitTill(() => delayIndicator.IsOffScreen);
        }

        private void FillFileDialog(string dialogTitle, string file)
        {
            Window fileOpenDialog = mainWindow.ModalWindow(dialogTitle);
            fileOpenDialog.Get<TextBox>(SearchCriteria.ByAutomationId("1148") /* File name: */).Text = file;
            fileOpenDialog.Get<Button>(SearchCriteria.ByAutomationId("1") /* Open */).Click();
        }

        private void CheckResults([CallerMemberName] string testCaseName = null)
        {
            string solutionDir = Path.GetDirectoryName(solution);
            IUIItem outputWindow = mainWindow.Get(SearchCriteria.ByText("Output").AndByClassName("GenericPane"), TimeSpan.FromSeconds(10));
            string testResults = new TestRunSerializer().ParseTestResults(solutionDir, testExplorer, outputWindow).ToXML();
            CheckResults(testResults, testCaseName);
        }

        private void CheckResults(string result, string testCaseName)
        {
            string expectationFile = Path.Combine(uiTestsPath, "UITestResults", this.GetType().Name + "__" + testCaseName + ".xml");
            string resultFile = Path.Combine(uiTestsPath, "TestErrors", this.GetType().Name + "__" + testCaseName + ".xml");

            if (!File.Exists(expectationFile))
            {
                File.WriteAllText(expectationFile, result);
                Assert.Inconclusive("This is the first time this test runs.");
            }

            string expectedResult = File.ReadAllText(expectationFile);
            string msg;
            bool stringsAreEqual = AreEqual(expectedResult, result, out msg);
            if (!stringsAreEqual)
            {
#pragma warning disable CS0162 // Unreachable code (because overwriteTestResults is compile time constant)
                if (overwriteTestResults)
                {
                    File.WriteAllText(expectationFile, result);
                    Assert.Inconclusive("Test results changed and have been overwritten. Differences: " + msg);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(resultFile));
                    File.WriteAllText(resultFile, result);
                    Assert.Fail("Test result doesn't match expectation. Result written to: " + resultFile + ". Differences: " + msg);
                }
#pragma warning restore CS0162
            }
            else if (stringsAreEqual && File.Exists(resultFile))
            {
                File.Delete(resultFile);
            }
        }

        private bool AreEqual(string expectedResult, string result, out string msg)
        {
            // normalize file endings
            expectedResult = Regex.Replace(expectedResult, @"\r\n|\n\r|\n|\r", "\r\n");
            result = Regex.Replace(result, @"\r\n|\n\r|\n|\r", "\r\n");

            bool areEqual = true;
            List<string> messages = new List<string>();
            if (expectedResult.Length != result.Length)
            {
                areEqual = false;
                messages.Add($"Length differs, expected: {expectedResult.Length}, actual: {result.Length}");
            }

            for (int i = 0; i < Math.Min(expectedResult.Length, result.Length); i++)
            {
                if (expectedResult[i] != result[i])
                {
                    areEqual = false;
                    messages.Add($"First difference at position {i}, expected: {expectedResult[i]}, "
                        + "actual: {result[i]}");
                    break;
                }
            }

            msg = string.Join("; ", messages);
            return areEqual;
        }

        private static void LogExceptionAndThrow(AutomationException exception, [CallerMemberName] string testCaseName = null)
        {
            string debugDetailsFile = Path.Combine(uiTestsPath, "TestErrors", typeof(UiTests).Name + "__" + testCaseName + "__DebugDetails.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(debugDetailsFile));
            File.WriteAllText(debugDetailsFile, exception.ToString() + "\r\n" + exception.StackTrace + "\r\n" + exception.DebugDetails);
            throw exception;
        }

        private static bool AskToCleanIfExists(VsExperimentalInstance visualStudioInstance)
        {
            bool keepDirtyInstance = false;
            if (visualStudioInstance.Exists())
            {
                var instanceExists = $"The experimental instance '{visualStudioInstance.VersionAndSuffix}' already exists.";
                var willReset = "\nShould it be deleted before going on with the tests?";

                MessageBoxResult result = MessageBoxWithTimeout.Show(instanceExists + willReset, "Warning!", MessageBoxButton.YesNoCancel, MessageBoxResult.Cancel);
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        visualStudioInstance.Clean();
                        break;
                    case MessageBoxResult.No:
                        keepDirtyInstance = true;
                        break;
                    case MessageBoxResult.Cancel:
                        Assert.Fail(instanceExists + " Didn't get confirmation to reset experimental instance. Cancelling...");
                        break;
                }
            }
            return keepDirtyInstance;
        }

    }

}