// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Validation;
using FluentAssertions;
using UnitTests.Validation.Setup;
using Xunit;

namespace UnitTests.Validation
{
    public class ResourceValidation
    {
        private const string Category = "Resource Validation";

        private List<IdentityResource> _identityResources = new List<IdentityResource>
        {
            new IdentityResource
            {
                Name = "openid",
                Required = true
            },
            new IdentityResource
            {
                Name = "email"
            }
        };

        private List<ApiResource> _apiResources = new List<ApiResource>
        {
            new ApiResource
            {
                Name = "resource1",
                Scopes = { "scope1", "scope2" }
            },
            new ApiResource
            {
                Name = "resource2",
                Scopes = { "disabled_scope" }
            },
        };

        private List<ApiScope> _scopes = new List<ApiScope> {
            new ApiScope
            {
                Name = "scope1",
                Required = true
            },
            new ApiScope
            {
                Name = "scope2"
            },
            new ApiScope
            {
                Name = "disabled_scope",
                Enabled = false,
            },
        };

        private Client _restrictedClient = new Client
        {
            ClientId = "restricted",

            AllowedScopes = new List<string>
            {
                "openid",
                "scope1",
                "disabled_scope"
            }
        };

        private IResourceStore _subject;

        public ResourceValidation()
        {
            _subject = new InMemoryResourcesStore(_identityResources, _apiResources, _scopes);
        }

        // scope validation

        [Fact]
        [Trait("Category", Category)]
        public async Task Only_Offline_Access_Requested()
        {
            var validator = Factory.CreateResourceValidator(_subject);
            var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
            {
                Client = _restrictedClient,
                Scopes = new[] { "offline_access" }
            });

            result.Succeeded.Should().BeFalse();
            result.InvalidScopes.Should().Contain("offline_access");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task All_Scopes_Valid()
        {
            var validator = Factory.CreateResourceValidator(_subject);
            var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
            {
                Client = _restrictedClient,
                Scopes = new[] { "openid", "scope1" }
            });

            result.Succeeded.Should().BeTrue();
            result.InvalidScopes.Should().BeEmpty();
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Invalid_Scope()
        {
            {
                var validator = Factory.CreateResourceValidator(_subject);
                var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
                {
                    Client = _restrictedClient,
                    Scopes = new[] { "openid", "email", "scope1", "unknown" }
                });

                result.Succeeded.Should().BeFalse();
                result.InvalidScopes.Should().Contain("unknown");
                result.InvalidScopes.Should().Contain("email");
            }
            {
                var validator = Factory.CreateResourceValidator(_subject);
                var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
                {
                    Client = _restrictedClient,
                    Scopes = new[] { "openid", "scope1", "scope2" }
                });

                result.Succeeded.Should().BeFalse();
                result.InvalidScopes.Should().Contain("scope2");
            }
            {
                var validator = Factory.CreateResourceValidator(_subject);
                var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
                {
                    Client = _restrictedClient,
                    Scopes = new[] { "openid", "email", "scope1" }
                });

                result.Succeeded.Should().BeFalse();
                result.InvalidScopes.Should().Contain("email");
            }
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Disabled_Scope()
        {
            var validator = Factory.CreateResourceValidator(_subject);
            var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
            {
                Client = _restrictedClient,
                Scopes = new[] { "openid", "scope1", "disabled_scope" }
            });

            result.Succeeded.Should().BeFalse();
            result.InvalidScopes.Should().Contain("disabled_scope");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task All_Scopes_Allowed_For_Restricted_Client()
        {
            var validator = Factory.CreateResourceValidator(_subject);
            var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
            {
                Client = _restrictedClient,
                Scopes = new[] { "openid", "scope1" }
            });

            result.Succeeded.Should().BeTrue();
            result.InvalidScopes.Should().BeEmpty();
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Restricted_Scopes()
        {
            var validator = Factory.CreateResourceValidator(_subject);
            var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
            {
                Client = _restrictedClient,
                Scopes = new[] { "openid", "email", "scope1", "scope2" }
            });

            result.Succeeded.Should().BeFalse();
            result.InvalidScopes.Should().Contain("email");
            result.InvalidScopes.Should().Contain("scope2");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Contains_Resource_and_Identity_Scopes()
        {
            var validator = Factory.CreateResourceValidator(_subject);
            var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
            {
                Client = _restrictedClient,
                Scopes = new[] { "openid", "scope1" }
            });

            result.Succeeded.Should().BeTrue();
            result.Resources.IdentityResources.SelectMany(x => x.Name).Should().Contain("openid");
            result.Resources.ApiScopes.Select(x => x.Name).Should().Contain("scope1");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Contains_Resource_Scopes_Only()
        {
            var validator = Factory.CreateResourceValidator(_subject);
            var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
            {
                Client = _restrictedClient,
                Scopes = new[] { "scope1" }
            });

            result.Succeeded.Should().BeTrue();
            result.Resources.IdentityResources.Should().BeEmpty();
            result.Resources.ApiScopes.Select(x => x.Name).Should().Contain("scope1");
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Contains_Identity_Scopes_Only()
        {
            var validator = Factory.CreateResourceValidator(_subject);
            var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
            {
                Client = _restrictedClient,
                Scopes = new[] { "openid" }
            });

            result.Succeeded.Should().BeTrue();
            result.Resources.IdentityResources.SelectMany(x => x.Name).Should().Contain("openid");
            result.Resources.ApiResources.Should().BeEmpty();
        }

        [Fact]
        [Trait("Category", Category)]
        public async Task Scope_matches_multipls_apis_should_succeed()
        {
            _apiResources.Clear();
            _apiResources.Add(new ApiResource { Name = "r1", Scopes = { "s" } });
            _apiResources.Add(new ApiResource { Name = "r2", Scopes = { "s" } });
            _scopes.Clear();
            _scopes.Add(new ApiScope("s"));

            var validator = Factory.CreateResourceValidator(_subject);
            var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
            {
                Client = new Client { AllowedScopes = { "s" } },
                Scopes = new[] { "s" }
            });

            result.Succeeded.Should().BeTrue();
            result.Resources.ApiResources.Count.Should().Be(2);
            result.Resources.ApiResources.Select(x => x.Name).Should().BeEquivalentTo(new[] { "r1", "r2" });
            result.RawScopeValues.Count().Should().Be(1);
            result.RawScopeValues.Should().BeEquivalentTo(new[] { "s" });
        }

    }
}