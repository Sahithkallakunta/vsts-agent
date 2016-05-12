using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Agent.Worker;
using Microsoft.VisualStudio.Services.Agent.Worker.Build;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Services.Agent.Tests.Worker.Build
{
    public sealed class GitSourceProviderL0
    {
        private Mock<IGitCommandManager> GetDefaultGitCommandMock()
        {
            Mock<IGitCommandManager> _gitCommandManager = new Mock<IGitCommandManager>();
            _gitCommandManager
                .Setup(x => x.GitInit(It.IsAny<IExecutionContext>(), It.IsAny<string>()))
                .Returns(Task.FromResult<int>(0));
            _gitCommandManager
                .Setup(x => x.GitRemoteAdd(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult<int>(0));
            _gitCommandManager
                .Setup(x => x.GitFetch(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<int>(0));
            _gitCommandManager
                .Setup(x => x.GitCheckout(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<int>(0));
            _gitCommandManager
                .Setup(x => x.GitClean(It.IsAny<IExecutionContext>(), It.IsAny<string>()))
                .Returns(Task.FromResult<int>(0));
            _gitCommandManager
                .Setup(x => x.GitReset(It.IsAny<IExecutionContext>(), It.IsAny<string>()))
                .Returns(Task.FromResult<int>(0));
            _gitCommandManager
                .Setup(x => x.GitRemoteSetUrl(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult<int>(0));
            _gitCommandManager
                .Setup(x => x.GitRemoteSetPushUrl(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult<int>(0));
            _gitCommandManager
                .Setup(x => x.GitSubmoduleInit(It.IsAny<IExecutionContext>(), It.IsAny<string>()))
                .Returns(Task.FromResult<int>(0));
            _gitCommandManager
                .Setup(x => x.GitSubmoduleUpdate(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<int>(0));
            _gitCommandManager
                .Setup(x => x.GitGetFetchUrl(It.IsAny<IExecutionContext>(), It.IsAny<string>()))
                .Returns(Task.FromResult<Uri>(new Uri("https://github.com/Microsoft/vsts-agent")));
            _gitCommandManager
                .Setup(x => x.GitDisableAutoGC(It.IsAny<IExecutionContext>(), It.IsAny<string>()))
                .Returns(Task.FromResult<int>(0));
            _gitCommandManager
                .Setup(x => x.GitVersion(It.IsAny<IExecutionContext>()))
                .Returns(Task.FromResult<Version>(new Version(2, 7)));

            return _gitCommandManager;
        }

        private Mock<IExecutionContext> GetTestExecutionContext(TestHostContext tc, string sourceFolder, string sourceBranch, string sourceVersion, bool enableAuth)
        {
            var trace = tc.GetTrace();
            var executionContext = new Mock<IExecutionContext>();
            List<string> warnings;
            executionContext
                .Setup(x => x.Variables)
                .Returns(new Variables(tc, copy: new Dictionary<string, string>(), maskHints: new List<MaskHint>(), warnings: out warnings));
            executionContext
                .Setup(x => x.Write(It.IsAny<string>(), It.IsAny<string>()))
                .Callback((string tag, string message) =>
                {
                    trace.Info($"{tag}{message}");
                });
            executionContext
                .Setup(x => x.WriteDebug)
                .Returns(true);
            executionContext.Object.Variables.Set(Constants.Variables.Build.SourcesDirectory, sourceFolder);
            executionContext.Object.Variables.Set(Constants.Variables.Build.SourceBranch, sourceBranch);
            executionContext.Object.Variables.Set(Constants.Variables.Build.SourceVersion, sourceVersion);
            executionContext.Object.Variables.Set(Constants.Variables.System.EnableAccessToken, enableAuth.ToString());

            return executionContext;
        }

        private ServiceEndpoint GetTestSourceEndpoint(string url, bool clean, bool checkoutSubmodules)
        {
            var endpoint = new ServiceEndpoint();
            endpoint.Data[WellKnownEndpointData.Clean] = clean.ToString();
            endpoint.Data[WellKnownEndpointData.CheckoutSubmodules] = checkoutSubmodules.ToString();
            endpoint.Url = new Uri(url);
            endpoint.Authorization = new EndpointAuthorization()
            {
                Scheme = EndpointAuthorizationSchemes.UsernamePassword
            };
            endpoint.Authorization.Parameters[EndpointAuthorizationParameters.Username] = "someuser";
            endpoint.Authorization.Parameters[EndpointAuthorizationParameters.Password] = "SomePassword!";

            return endpoint;
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void GetSourceGitClone()
        {
            using (TestHostContext tc = new TestHostContext(this))
            {
                // Arrange.
                string dumySourceFolder = Path.Combine(IOUtil.GetBinPath(), "SourceProviderL0");
                var executionContext = GetTestExecutionContext(tc, dumySourceFolder, "master", "a596e13f5db8869f44574be0392fb8fe1e790ce4", false);
                var endpoint = GetTestSourceEndpoint("https://github.com/Microsoft/vsts-agent", false, false);

                var _gitCommandManager = GetDefaultGitCommandMock();
                tc.SetSingleton<IGitCommandManager>(_gitCommandManager.Object);
                tc.SetSingleton<IWhichUtil>(new WhichUtil());

                GitSourceProvider gitSourceProvider = new GitSourceProvider();
                gitSourceProvider.Initialize(tc);

                // Act.
                gitSourceProvider.GetSourceAsync(executionContext.Object, endpoint, default(CancellationToken)).GetAwaiter().GetResult();

                // Assert.
                _gitCommandManager.Verify(x => x.GitInit(executionContext.Object, dumySourceFolder));
                _gitCommandManager.Verify(x => x.GitRemoteAdd(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                _gitCommandManager.Verify(x => x.GitRemoteSetUrl(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                _gitCommandManager.Verify(x => x.GitRemoteSetPushUrl(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                _gitCommandManager.Verify(x => x.GitCheckout(executionContext.Object, dumySourceFolder, "a596e13f5db8869f44574be0392fb8fe1e790ce4", It.IsAny<CancellationToken>()));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void GetSourceGitFetch()
        {
            using (TestHostContext tc = new TestHostContext(this))
            {
                var trace = tc.GetTrace();
                // Arrange.
                string dumySourceFolder = Path.Combine(IOUtil.GetBinPath(), "SourceProviderL0");
                try
                {
                    Directory.CreateDirectory(dumySourceFolder);
                    string dumyGitFolder = Path.Combine(dumySourceFolder, ".git");
                    Directory.CreateDirectory(dumyGitFolder);
                    string dumyGitConfig = Path.Combine(dumyGitFolder, "config");
                    File.WriteAllText(dumyGitConfig, "test git confg file");

                    var executionContext = GetTestExecutionContext(tc, dumySourceFolder, "master", "a596e13f5db8869f44574be0392fb8fe1e790ce4", false);
                    var endpoint = GetTestSourceEndpoint("https://github.com/Microsoft/vsts-agent", false, false);

                    var _gitCommandManager = GetDefaultGitCommandMock();
                    tc.SetSingleton<IGitCommandManager>(_gitCommandManager.Object);
                    tc.SetSingleton<IWhichUtil>(new WhichUtil());

                    GitSourceProvider gitSourceProvider = new GitSourceProvider();
                    gitSourceProvider.Initialize(tc);

                    // Act.
                    gitSourceProvider.GetSourceAsync(executionContext.Object, endpoint, default(CancellationToken)).GetAwaiter().GetResult();

                    // Assert.
                    _gitCommandManager.Verify(x => x.GitDisableAutoGC(executionContext.Object, dumySourceFolder));
                    _gitCommandManager.Verify(x => x.GitRemoteSetUrl(executionContext.Object, dumySourceFolder, "origin", "https://someuser:SomePassword%21@github.com/Microsoft/vsts-agent"));
                    _gitCommandManager.Verify(x => x.GitRemoteSetPushUrl(executionContext.Object, dumySourceFolder, "origin", "https://someuser:SomePassword%21@github.com/Microsoft/vsts-agent"));
                    _gitCommandManager.Verify(x => x.GitFetch(executionContext.Object, dumySourceFolder, "origin", It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));
                    _gitCommandManager.Verify(x => x.GitRemoteSetUrl(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                    _gitCommandManager.Verify(x => x.GitRemoteSetPushUrl(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                    _gitCommandManager.Verify(x => x.GitCheckout(executionContext.Object, dumySourceFolder, "a596e13f5db8869f44574be0392fb8fe1e790ce4", It.IsAny<CancellationToken>()));
                }
                finally
                {
                    IOUtil.DeleteDirectory(dumySourceFolder, CancellationToken.None);
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void GetSourceGitClonePR()
        {
            using (TestHostContext tc = new TestHostContext(this))
            {
                var trace = tc.GetTrace();
                // Arrange.
                string dumySourceFolder = Path.Combine(IOUtil.GetBinPath(), "SourceProviderL0");
                var executionContext = GetTestExecutionContext(tc, dumySourceFolder, "refs/pull/12345", "a596e13f5db8869f44574be0392fb8fe1e790ce4", false);
                var endpoint = GetTestSourceEndpoint("https://github.com/Microsoft/vsts-agent", false, false);

                var _gitCommandManager = GetDefaultGitCommandMock();
                tc.SetSingleton<IGitCommandManager>(_gitCommandManager.Object);
                tc.SetSingleton<IWhichUtil>(new WhichUtil());

                GitSourceProvider gitSourceProvider = new GitSourceProvider();
                gitSourceProvider.Initialize(tc);

                // Act.
                gitSourceProvider.GetSourceAsync(executionContext.Object, endpoint, default(CancellationToken)).GetAwaiter().GetResult();

                // Assert.
                _gitCommandManager.Verify(x => x.GitInit(executionContext.Object, dumySourceFolder));
                _gitCommandManager.Verify(x => x.GitRemoteAdd(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                _gitCommandManager.Verify(x => x.GitFetch(executionContext.Object, dumySourceFolder, "origin", new List<string>() { "+refs/pull/12345:refs/remotes/pull/12345" }, It.IsAny<string>(), It.IsAny<CancellationToken>()));
                _gitCommandManager.Verify(x => x.GitRemoteSetUrl(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                _gitCommandManager.Verify(x => x.GitRemoteSetPushUrl(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                _gitCommandManager.Verify(x => x.GitCheckout(executionContext.Object, dumySourceFolder, It.Is<string>(s => s.Equals("refs/remotes/pull/12345")), It.IsAny<CancellationToken>()));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void GetSourceGitFetchPR()
        {
            using (TestHostContext tc = new TestHostContext(this))
            {
                var trace = tc.GetTrace();
                // Arrange.
                string dumySourceFolder = Path.Combine(IOUtil.GetBinPath(), "SourceProviderL0");
                try
                {
                    Directory.CreateDirectory(dumySourceFolder);
                    string dumyGitFolder = Path.Combine(dumySourceFolder, ".git");
                    Directory.CreateDirectory(dumyGitFolder);
                    string dumyGitConfig = Path.Combine(dumyGitFolder, "config");
                    File.WriteAllText(dumyGitConfig, "test git confg file");

                    var executionContext = GetTestExecutionContext(tc, dumySourceFolder, "refs/pull/12345/merge", "a596e13f5db8869f44574be0392fb8fe1e790ce4", false);
                    var endpoint = GetTestSourceEndpoint("https://github.com/Microsoft/vsts-agent", false, false);

                    var _gitCommandManager = GetDefaultGitCommandMock();
                    tc.SetSingleton<IGitCommandManager>(_gitCommandManager.Object);
                    tc.SetSingleton<IWhichUtil>(new WhichUtil());

                    GitSourceProvider gitSourceProvider = new GitSourceProvider();
                    gitSourceProvider.Initialize(tc);

                    // Act.
                    gitSourceProvider.GetSourceAsync(executionContext.Object, endpoint, default(CancellationToken)).GetAwaiter().GetResult();

                    // Assert.
                    _gitCommandManager.Verify(x => x.GitDisableAutoGC(executionContext.Object, dumySourceFolder));
                    _gitCommandManager.Verify(x => x.GitRemoteSetUrl(executionContext.Object, dumySourceFolder, "origin", "https://someuser:SomePassword%21@github.com/Microsoft/vsts-agent"));
                    _gitCommandManager.Verify(x => x.GitRemoteSetPushUrl(executionContext.Object, dumySourceFolder, "origin", "https://someuser:SomePassword%21@github.com/Microsoft/vsts-agent"));
                    _gitCommandManager.Verify(x => x.GitFetch(executionContext.Object, dumySourceFolder, "origin", new List<string>() { "+refs/pull/12345/merge:refs/remotes/pull/12345/merge" }, It.IsAny<string>(), It.IsAny<CancellationToken>()));
                    _gitCommandManager.Verify(x => x.GitRemoteSetUrl(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                    _gitCommandManager.Verify(x => x.GitRemoteSetPushUrl(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                    _gitCommandManager.Verify(x => x.GitCheckout(executionContext.Object, dumySourceFolder, "refs/remotes/pull/12345/merge", It.IsAny<CancellationToken>()));
                }
                finally
                {
                    IOUtil.DeleteDirectory(dumySourceFolder, CancellationToken.None);
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void GetSourceReCloneOnUrlNotMatch()
        {
            using (TestHostContext tc = new TestHostContext(this))
            {
                var trace = tc.GetTrace();
                // Arrange.
                string dumySourceFolder = Path.Combine(IOUtil.GetBinPath(), "SourceProviderL0");
                try
                {
                    Directory.CreateDirectory(dumySourceFolder);
                    string dumyGitFolder = Path.Combine(dumySourceFolder, ".git");
                    Directory.CreateDirectory(dumyGitFolder);
                    string dumyGitConfig = Path.Combine(dumyGitFolder, "config");
                    File.WriteAllText(dumyGitConfig, "test git confg file");

                    var executionContext = GetTestExecutionContext(tc, dumySourceFolder, "refs/heads/users/user1", "", true);
                    var endpoint = GetTestSourceEndpoint("https://github.com/Microsoft/vsts-agent", false, false);

                    var _gitCommandManager = GetDefaultGitCommandMock();
                    _gitCommandManager
                        .Setup(x => x.GitGetFetchUrl(It.IsAny<IExecutionContext>(), It.IsAny<string>()))
                        .Returns(Task.FromResult<Uri>(new Uri("https://github.com/Microsoft/vsts-another-agent")));

                    tc.SetSingleton<IGitCommandManager>(_gitCommandManager.Object);
                    tc.SetSingleton<IWhichUtil>(new WhichUtil());

                    GitSourceProvider gitSourceProvider = new GitSourceProvider();
                    gitSourceProvider.Initialize(tc);

                    // Act.
                    gitSourceProvider.GetSourceAsync(executionContext.Object, endpoint, default(CancellationToken)).GetAwaiter().GetResult();

                    // Assert.
                    _gitCommandManager.Verify(x => x.GitInit(executionContext.Object, dumySourceFolder));
                    _gitCommandManager.Verify(x => x.GitRemoteAdd(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                    _gitCommandManager.Verify(x => x.GitCheckout(executionContext.Object, dumySourceFolder, "refs/remotes/origin/users/user1", It.IsAny<CancellationToken>()));
                }
                finally
                {
                    IOUtil.DeleteDirectory(dumySourceFolder, CancellationToken.None);
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void GetSourceGitFetchWithClean()
        {
            using (TestHostContext tc = new TestHostContext(this))
            {
                var trace = tc.GetTrace();
                // Arrange.
                string dumySourceFolder = Path.Combine(IOUtil.GetBinPath(), "SourceProviderL0");
                try
                {
                    Directory.CreateDirectory(dumySourceFolder);
                    string dumyGitFolder = Path.Combine(dumySourceFolder, ".git");
                    Directory.CreateDirectory(dumyGitFolder);
                    string dumyGitConfig = Path.Combine(dumyGitFolder, "config");
                    File.WriteAllText(dumyGitConfig, "test git confg file");

                    var executionContext = GetTestExecutionContext(tc, dumySourceFolder, "refs/remotes/origin/master", "", false);
                    var endpoint = GetTestSourceEndpoint("https://github.com/Microsoft/vsts-agent", true, false);

                    var _gitCommandManager = GetDefaultGitCommandMock();
                    tc.SetSingleton<IGitCommandManager>(_gitCommandManager.Object);
                    tc.SetSingleton<IWhichUtil>(new WhichUtil());

                    GitSourceProvider gitSourceProvider = new GitSourceProvider();
                    gitSourceProvider.Initialize(tc);

                    // Act.
                    gitSourceProvider.GetSourceAsync(executionContext.Object, endpoint, default(CancellationToken)).GetAwaiter().GetResult();

                    // Assert.
                    _gitCommandManager.Verify(x => x.GitClean(executionContext.Object, dumySourceFolder));
                    _gitCommandManager.Verify(x => x.GitReset(executionContext.Object, dumySourceFolder));
                    _gitCommandManager.Verify(x => x.GitDisableAutoGC(executionContext.Object, dumySourceFolder));
                    _gitCommandManager.Verify(x => x.GitRemoteSetUrl(executionContext.Object, dumySourceFolder, "origin", It.Is<string>(s => s.Equals("https://someuser:SomePassword%21@github.com/Microsoft/vsts-agent"))));
                    _gitCommandManager.Verify(x => x.GitRemoteSetPushUrl(executionContext.Object, dumySourceFolder, "origin", It.Is<string>(s => s.Equals("https://someuser:SomePassword%21@github.com/Microsoft/vsts-agent"))));
                    _gitCommandManager.Verify(x => x.GitFetch(executionContext.Object, dumySourceFolder, "origin", It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));
                    _gitCommandManager.Verify(x => x.GitRemoteSetUrl(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                    _gitCommandManager.Verify(x => x.GitRemoteSetPushUrl(executionContext.Object, dumySourceFolder, "origin", "https://github.com/Microsoft/vsts-agent"));
                    _gitCommandManager.Verify(x => x.GitCheckout(executionContext.Object, dumySourceFolder, "refs/remotes/origin/master", It.IsAny<CancellationToken>()));
                }
                finally
                {
                    IOUtil.DeleteDirectory(dumySourceFolder, CancellationToken.None);
                }
            }
        }
    }
}
