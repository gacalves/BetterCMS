﻿//using System;
//using System.IO;
//using System.Net;

//using BetterCms.Core.Services.Storage;

//using Microsoft.WindowsAzure.Storage;
//using Microsoft.WindowsAzure.Storage.Auth;

//using StorageException = BetterCms.Core.Exceptions.Service.StorageException;

//namespace BetterCms.Module.AmazonS3Storage
//{
//    public class WindowsAzureStorageService : IStorageService
//    { 
//        private readonly string accessKey;
//        private readonly string secretKey;
//        private readonly string bucketName;

//        public WindowsAzureStorageService(ICmsConfiguration config)
//        {
//            try            
//            {  
//              //  CloudStorageAccount account = new CloudStorageAccount(new StorageCredentials(), );
                
//                var serviceSection = config.Storage;

//                accessKey = serviceSection.GetValue("AmazonAccessKey");
//                secretKey = serviceSection.GetValue("AmazonSecretKey");
//                bucketName = serviceSection.GetValue("AmazonBucketName");
//            }
//            catch (Exception e)
//            {
//                throw new StorageException(string.Format("Failed to initialize storage service {0}.", GetType()), e);
//            }
//        }

//        public bool ObjectExists(Uri uri)
//        {
//            CheckUri(uri);

//            try
//            {
//                using (var client = CreateAmazonS3Client())
//                {
//                    try
//                    {
//                        var absolutePath = uri.AbsolutePath;
//                        var key = absolutePath.TrimStart(Convert.ToChar("/"));
//                        var request = new GetObjectMetadataRequest();

//                        request.WithBucketName(bucketName)
//                            .WithKey(key);

//                        using (client.GetObjectMetadata(request))
//                        {
//                            return true;
//                        }
//                    }
//                    catch (AmazonS3Exception ex)
//                    {
//                        if (ex.StatusCode == HttpStatusCode.NotFound)
//                        {
//                            return false;
//                        }

//                        // Status not found - throw the exception.
//                        throw;
//                    }
//                }
//            }
//            catch (Exception e)
//            {
//                throw new StorageException(string.Format("Failed to check if object exists {0}.", uri), e);
//            }
//        }

//        public void UploadObject(UploadRequest request)
//        {
//            CheckUri(request.Uri);

//            try
//            {
//                var putRequest = new PutObjectRequest();
//                using (var client = CreateAmazonS3Client())
//                {
//                    var absolutePath = request.Uri.AbsolutePath;
//                    var key = absolutePath.TrimStart(Convert.ToChar("/"));

//                    putRequest.WithBucketName(bucketName)
//                        .WithKey(key)
//                        .WithCannedACL(S3CannedACL.PublicRead)
//                        .WithInputStream(request.InputStream);

//                    if (request.Headers != null && request.Headers.Count > 0)
//                    {
//                        putRequest.AddHeaders(request.Headers);
//                    }

//                    if (request.MetaData != null && request.MetaData.Count > 0)
//                    {
//                        putRequest.WithMetaData(request.MetaData);
//                    }

//                    using (client.PutObject(putRequest))
//                    {
//                    }
//                }
//            }
//            catch (Exception e)
//            {
//                throw new StorageException(string.Format("Failed to upload object with request {0}.", request), e);
//            }
//        }

//        public DownloadResponse DownloadObject(Uri uri)
//        {
//            CheckUri(uri);

//            try
//            {                
//                var request = (HttpWebRequest)WebRequest.Create(uri);
//                var response = request.GetResponse();
//                var downloadResponse = new DownloadResponse();
//                downloadResponse.Uri = uri;

//                using (var responseStream = response.GetResponseStream())
//                {
//                    downloadResponse.ResponseStream = new MemoryStream();
//                    if (responseStream != null)
//                    {
//                        responseStream.CopyTo(downloadResponse.ResponseStream);
//                    }
//                }

//                return downloadResponse;
//            }
//            catch (Exception e)
//            {
//                throw new StorageException(string.Format("Failed to download object from {0}.", uri), e);
//            }
//        }

//        public void CopyObject(Uri sourceUri, Uri destinationUri)
//        {
//            CheckUri(sourceUri);
//            CheckUri(destinationUri);

//            try
//            {
//                var sourceKey = sourceUri.AbsolutePath.TrimStart('/');
//                var destinationKey = destinationUri.AbsolutePath.TrimStart('/');

//                using (var client = CreateAmazonS3Client())
//                {
//                    var request = new CopyObjectRequest()
//                        .WithSourceBucket(bucketName)
//                        .WithDestinationBucket(bucketName)
//                        .WithCannedACL(S3CannedACL.PublicRead)
//                        .WithSourceKey(sourceKey)
//                        .WithDestinationKey(destinationKey)
//                        .WithDirective(S3MetadataDirective.COPY);

//                    client.CopyObject(request);

//                }
//            }
//            catch (Exception e)
//            {
//                throw new StorageException(string.Format("Failed to copy object. SourceUrl: {0}, DestinationUrl: {1}", sourceUri, destinationUri), e);
//            }
//        }

//        public void RemoveObject(Uri uri)
//        {
//            CheckUri(uri);

//            try
//            {
//                var sourceKey = uri.AbsolutePath.TrimStart('/');                

//                using (var client = CreateAmazonS3Client())
//                {
//                    var request = new DeleteObjectRequest()
//                        .WithKey(sourceKey)
//                        .WithBucketName(bucketName);

//                    client.DeleteObject(request);                    
//                }
//            }
//            catch (Exception e)
//            {
//                throw new StorageException(string.Format("Failed to delete object. Uri: {0}", uri), e);
//            }
//        }

//        public void RemoveObjectBucket(Uri uri)
//        {
//            CheckUri(uri);

//            try
//            {
//                var sourceKey = uri.AbsolutePath.TrimStart('/');

//                using (var client = CreateAmazonS3Client())
//                {
//                    var request = new DeleteObjectRequest()
//                        .WithKey(sourceKey)
//                        .WithBucketName(bucketName);

//                    client.DeleteObject(request);
//                }
//            }
//            catch (Exception e)
//            {
//                throw new StorageException(string.Format("Failed to delete object. Uri: {0}", uri), e);
//            }
//        }

//        private AmazonS3 CreateAmazonS3Client()
//        {
//            return AWSClientFactory.CreateAmazonS3Client(accessKey, secretKey);
//        }

//        private void CheckUri(Uri uri)
//        {
//            if (!Uri.CheckSchemeName(uri.Scheme) || !(uri.Scheme.Equals(Uri.UriSchemeHttp) || uri.Scheme.Equals(Uri.UriSchemeHttps)))
//            {
//                throw new StorageException(string.Format("An Uri scheme {0} is invalid. Uri {1} can't be processed with a {2} storage service.", uri.Scheme, uri, GetType().Name));
//            }
//        } 
//    }
//}
