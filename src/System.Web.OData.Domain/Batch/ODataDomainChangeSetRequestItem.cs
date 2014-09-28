﻿// Domain Framework ver. 1.0
// Copyright (c) Microsoft Corporation
// All rights reserved.
// MIT License
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
// associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute,
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial
// portions of the Software.
// 
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
// NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES
// OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.OData.Batch;
using Microsoft.Data.Domain;
using Microsoft.Data.Domain.Submit;
using DomainEngine = Microsoft.Data.Domain.Domain;

namespace System.Web.OData.Domain.Batch
{
    public class ODataDomainChangeSetRequestItem : ChangeSetRequestItem
    {
        private DomainContext context;

        public ODataDomainChangeSetRequestItem(IEnumerable<HttpRequestMessage> requests, DomainContext context)
            : base(requests)
        {
            Ensure.NotNull(context, "context");

            this.context = context;
        }

        public override async Task<ODataBatchResponseItem> SendRequestAsync(HttpMessageInvoker invoker, CancellationToken cancellationToken)
        {
            Ensure.NotNull(invoker, "invoker");

            ODataDomainChangeSetProperty changeSetProperty = new ODataDomainChangeSetProperty(this);
            changeSetProperty.ChangeSet = new ChangeSet();
            this.SetChangeSetProperty(changeSetProperty);

            Dictionary<string, string> contentIdToLocationMapping = new Dictionary<string, string>();
            List<Task<HttpResponseMessage>> responseTasks = new List<Task<HttpResponseMessage>>();
            foreach (HttpRequestMessage request in Requests)
            {
                responseTasks.Add(SendMessageAsync(invoker, request, cancellationToken, contentIdToLocationMapping));
            }

            // the responseTasks will be complete after:
            // - the ChangeSet is submitted
            // - the responses are created and
            // - the controller actions have returned
            await Task.WhenAll(responseTasks);

            List<HttpResponseMessage> responses = new List<HttpResponseMessage>();
            try
            {
                foreach (Task<HttpResponseMessage> responseTask in responseTasks)
                {
                    HttpResponseMessage response = responseTask.Result;
                    if (response.IsSuccessStatusCode)
                    {
                        responses.Add(response);
                    }
                    else
                    {
                        DisposeResponses(responses);
                        responses.Clear();
                        responses.Add(response);
                        return new ChangeSetResponseItem(responses);
                    }
                }
            }
            catch
            {
                DisposeResponses(responses);
                throw;
            }

            return new ChangeSetResponseItem(responses);
        }

        internal async void SubmitChangeSet(ChangeSet changeSet, Action postSubmitAction)
        {
            SubmitResult submitResults = await DomainEngine.SubmitAsync(this.context, changeSet);

            postSubmitAction();
        }

        private void SetChangeSetProperty(ODataDomainChangeSetProperty changeSetProperty)
        {
            foreach (HttpRequestMessage request in this.Requests)
            {
                request.Properties.Add("Microsoft.Data.Domain.Submit.ChangeSet", changeSetProperty);
            }
        }

        private static void DisposeResponses(IEnumerable<HttpResponseMessage> responses)
        {
            foreach (HttpResponseMessage response in responses)
            {
                if (response != null)
                {
                    response.Dispose();
                }
            }
        }
    }
}