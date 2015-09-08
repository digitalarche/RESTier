﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Library;
using Microsoft.Restier.Core.Conventions;
using Microsoft.Restier.Core.Model;
using Xunit;

namespace Microsoft.Restier.Core.Tests
{
    public class ConventionalModelExtenderTest
    {
        [Theory]
        [InlineData(typeof(OneDomain))]
        [InlineData(typeof(OtherDomain))]
        public async Task ExtendModelAsync_UpdatesModel_IfHasOnModelCreatingMethod(Type type)
        {
            // Arrange
            var domain = Activator.CreateInstance(type);
            var model = GetModel();
            var extender = new ConventionalModelExtender(type) { InnerHandler = new TestModelProducer(model) };
            var domainConfig = new DomainConfiguration();
            domainConfig.EnsureCommitted();
            var domainContext = new DomainContext(domainConfig);
            domainContext.SetProperty(typeof(Domain).AssemblyQualifiedName, domain);

            // Act
            var result = await extender.GetModelAsync(new InvocationContext(domainContext), new CancellationToken());

            // Assert
            Assert.Same(model, result);
            var operations = model.SchemaElements.OfType<IEdmOperation>();
            Assert.Single(operations);
            var operation = operations.Single();
            Assert.True(operation.IsBound);
            Assert.True(operation.IsFunction());
            Assert.Equal("MostExpensive", operation.Name);
            Assert.Equal("ns", operation.Namespace);
        }

        [Fact]
        public async Task ExtendModelAsync_DoesntUpdatesModel_IfWithoutOnModelCreatingMethod()
        {
            // Arrange
            var domain = new AnyDomain();
            var type = domain.GetType();
            var model = GetModel();
            var extender = new ConventionalModelExtender(type) {InnerHandler = new TestModelProducer(model)};
            var domainConfig = new DomainConfiguration();
            domainConfig.EnsureCommitted();
            var domainContext = new DomainContext(domainConfig);
            domainContext.SetProperty(type.AssemblyQualifiedName, domain);

            // Act
            var result = await extender.GetModelAsync(new InvocationContext(domainContext), new CancellationToken());

            // Assert
            Assert.Same(model, result);
            Assert.Empty(model.SchemaElements.OfType<IEdmOperation>());
        }

        private static EdmModel GetModel()
        {
            var model = new EdmModel();
            var productType = new EdmEntityType("ns", "Product");
            model.AddElement(productType);
            return model;
        }

        public class Product
        {
            public int Id { get; set; }
        }

        public class OneDomain
        {
            private EdmModel OnModelExtending(EdmModel model)
            {
                return OtherDomain.OnModelExtending(model);
            }
        }

        public class OtherDomain
        {
            internal static EdmModel OnModelExtending(EdmModel model)
            {
                var ns = model.DeclaredNamespaces.First();
                var product = (IEdmEntityType)model.FindDeclaredType(ns + "." + "Product");
                var products = EdmCoreModel.GetCollection(new EdmEntityTypeReference(product, false));
                var mostExpensive = new EdmFunction(ns, "MostExpensive",
                    EdmCoreModel.Instance.GetPrimitive(EdmPrimitiveTypeKind.Double, isNullable: false), isBound: true,
                    entitySetPathExpression: null, isComposable: false);
                mostExpensive.AddParameter("bindingParameter", products);
                model.AddElement(mostExpensive);
                return model;
            }
        }

        public class AnyDomain
        {
        }

        public class TestModelProducer : IModelBuilder
        {
            public IEdmModel Model { get; set; }

            public TestModelProducer(IEdmModel model)
            {
                this.Model = model;
            }

            public Task<IEdmModel> GetModelAsync(InvocationContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult(this.Model);
            }
        }
    }
}
