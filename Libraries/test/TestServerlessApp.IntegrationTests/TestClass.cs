using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.Lambda;
using Amazon.S3;
using TestServerlessApp.IntegrationTests.Helpers;
using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    public class TestClass
    {
        private const string StackName = "test-serverless-app";
        private const string BucketName = "devex-test-serverless-app";

        private readonly CloudFormationHelper _cloudFormationHelper;
        private readonly S3Helper _s3Helper;
        private readonly LambdaHelper _lambdaHelper;
        private readonly string _restApiUrlPrefix;
        private readonly string _httpApiUrlPrefix;
        private readonly List<LambdaFunction> _lambdaFunctions;
        private readonly HttpClient _httpClient;

        public TestClass()
        {
            _cloudFormationHelper = new CloudFormationHelper(new AmazonCloudFormationClient());
            _s3Helper = new S3Helper(new AmazonS3Client());
            _lambdaHelper = new LambdaHelper(new AmazonLambdaClient());
            _restApiUrlPrefix = _cloudFormationHelper.GetOutputValue(StackName, "RestApiURL").GetAwaiter().GetResult();
            _httpApiUrlPrefix = _cloudFormationHelper.GetOutputValue(StackName, "HttpApiURL").GetAwaiter().GetResult();
            _lambdaFunctions = _lambdaHelper.FilterByCloudFormationStack(StackName).GetAwaiter().GetResult();
            _httpClient = new HttpClient();

            Assert.Equal(StackStatus.CREATE_COMPLETE, _cloudFormationHelper.GetStackStatus(StackName).GetAwaiter().GetResult());
            Assert.True(_s3Helper.BucketExists(BucketName).GetAwaiter().GetResult());
            Assert.Equal(11, _lambdaFunctions.Count);
            Assert.False(string.IsNullOrEmpty(_restApiUrlPrefix));
            Assert.False(string.IsNullOrEmpty(_httpApiUrlPrefix));
        }

        [Fact]
        public async Task TestMethod()
        {
            try
            {
                await VerifySimpleCalculator();
            }
            finally
            {
                await CleanUp();
            }
        }

        private async Task VerifySimpleCalculator()
        {
            // Add
            var response = await _httpClient.GetAsync($"{_restApiUrlPrefix}/SimpleCalculator/Add?x=2&y=4");
            response.EnsureSuccessStatusCode();
            Assert.Equal("6", await response.Content.ReadAsStringAsync());

            // Multiply
            response = await _httpClient.GetAsync($"{_restApiUrlPrefix}/SimpleCalculator/Multiply/2/10");
            response.EnsureSuccessStatusCode();
            Assert.Equal("20", await response.Content.ReadAsStringAsync());

            // Divide
            response = await _httpClient.GetAsync($"{_restApiUrlPrefix}/SimpleCalculator/DivideAsync/50/5");
            response.EnsureSuccessStatusCode();
            Assert.Equal("10", await response.Content.ReadAsStringAsync());

            // Subtract
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{_restApiUrlPrefix}/SimpleCalculator/Subtract"),
                Headers = {{ "x", "10" }, {"y", "2"}}
            };
            response = _httpClient.SendAsync(httpRequestMessage).Result;
            response.EnsureSuccessStatusCode();
            Assert.Equal("8", await response.Content.ReadAsStringAsync());
        }

        private async Task CleanUp()
        {
            await _cloudFormationHelper.DeleteStack(StackName);
            Assert.True(await _cloudFormationHelper.IsDeleted(StackName), $"The stack '{StackName}' still exists and will have to be manually deleted from the AWS console.");

            await _s3Helper.DeleteBucket(BucketName);
            Assert.False(await _s3Helper.BucketExists(BucketName), $"The bucket '{BucketName}' still exists and will have to be manually deleted from the AWS console.");
        }
    }
}