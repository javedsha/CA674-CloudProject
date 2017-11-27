﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Threading;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace FaceAPI_MVC.Web
{
    public class RESTHelper
    {
        protected bool IsTableStorage { get; set; }

        private string endpoint;
        public string Endpoint
        {
            get
            {
                return endpoint;
            }
            internal set
            {
                endpoint = value;
            }
        }

        private string storageAccount;
        public string StorageAccount
        {
            get
            {
                return storageAccount;
            }
            internal set
            {
                storageAccount = value;
            }
        }

        private string storageKey;
        public string StorageKey
        {
            get
            {
                return storageKey;
            }
            internal set
            {
                storageKey = value;
            }
        }


        public RESTHelper(string endpoint, string storageAccount, string storageKey)
        {
            this.Endpoint = endpoint;
            this.StorageAccount = storageAccount;
            this.StorageKey = storageKey;
        }


        #region REST HTTP Request Helper Methods

        // Construct and issue a REST request and return the response.

        public HttpWebRequest CreateRESTRequest(string method, string resource, string requestBody = null, SortedList<string, string> headers = null,
            string ifMatch = "", string md5 = "")
        {
            byte[] byteArray = null;
            DateTime now = DateTime.UtcNow;
            string uri = Endpoint + resource;

            HttpWebRequest request = HttpWebRequest.Create(uri) as HttpWebRequest;
            request.Method = method;
            request.ContentLength = 0;

            if (!uri.Contains("container"))
            {
                request.ContentType = "image/jpeg";
            }

            request.Headers.Add("x-ms-date", now.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            // request.Headers.Add("x-ms-version", "2009-09-19");

            if (IsTableStorage)
            {
                request.ContentType = "application/atom+xml";

                request.Headers.Add("DataServiceVersion", "1.0;NetFx");
                request.Headers.Add("MaxDataServiceVersion", "1.0;NetFx");
            }

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            if (!String.IsNullOrEmpty(requestBody))
            {
                request.Headers.Add("Accept-Charset", "UTF-8");

                byteArray = Encoding.UTF8.GetBytes(requestBody);
                request.ContentLength = byteArray.Length;
            }

            // request.Headers.Add("Authorization", AuthorizationHeader(method, now, request, ifMatch, md5));

            if (!String.IsNullOrEmpty(requestBody))
            {
                request.GetRequestStream().Write(byteArray, 0, byteArray.Length);
            }

            return request;
        }


        // Generate an authorization header.

        public string AuthorizationHeader(string method, DateTime now, HttpWebRequest request, string ifMatch = "", string md5 = "")
        {
            string MessageSignature;

            if (IsTableStorage)
            {
                MessageSignature = String.Format("{0}\n\n{1}\n{2}\n{3}",
                    method,
                    "application/atom+xml",
                    now.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    GetCanonicalizedResource(request.RequestUri, StorageAccount)
                    );
            }
            else
            {
                MessageSignature = String.Format("{0}\n\n\n{1}\n{5}\n\n\n\n{2}\n\n\n\n{3}{4}",
                    method,
                    (method == "GET" || method == "HEAD") ? String.Empty : request.ContentLength.ToString(),
                    ifMatch,
                    GetCanonicalizedHeaders(request),
                    GetCanonicalizedResource(request.RequestUri, StorageAccount),
                    md5
                    );
            }
            byte[] SignatureBytes = System.Text.Encoding.UTF8.GetBytes(MessageSignature);
            System.Security.Cryptography.HMACSHA256 SHA256 = new System.Security.Cryptography.HMACSHA256(Convert.FromBase64String(StorageKey));
            String AuthorizationHeader = "SharedKey " + StorageAccount + ":" + Convert.ToBase64String(SHA256.ComputeHash(SignatureBytes));
            return AuthorizationHeader;
        }

        // Get canonicalized headers.

        public string GetCanonicalizedHeaders(HttpWebRequest request)
        {
            ArrayList headerNameList = new ArrayList();
            StringBuilder sb = new StringBuilder();
            foreach (string headerName in request.Headers.Keys)
            {
                if (headerName.ToLowerInvariant().StartsWith("x-ms-", StringComparison.Ordinal))
                {
                    headerNameList.Add(headerName.ToLowerInvariant());
                }
            }
            headerNameList.Sort();
            foreach (string headerName in headerNameList)
            {
                StringBuilder builder = new StringBuilder(headerName);
                string separator = ":";
                foreach (string headerValue in GetHeaderValues(request.Headers, headerName))
                {
                    string trimmedValue = headerValue.Replace("\r\n", String.Empty);
                    builder.Append(separator);
                    builder.Append(trimmedValue);
                    separator = ",";
                }
                sb.Append(builder.ToString());
                sb.Append("\n");
            }
            return sb.ToString();
        }

        // Get header values.

        public ArrayList GetHeaderValues(NameValueCollection headers, string headerName)
        {
            ArrayList list = new ArrayList();
            string[] values = headers.GetValues(headerName);
            if (values != null)
            {
                foreach (string str in values)
                {
                    list.Add(str.TrimStart(null));
                }
            }
            return list;
        }

        // Get canonicalized resource.

        public string GetCanonicalizedResource(Uri address, string accountName)
        {
            StringBuilder str = new StringBuilder();
            StringBuilder builder = new StringBuilder("/");
            builder.Append(accountName);
            builder.Append(address.AbsolutePath);
            str.Append(builder.ToString());
            NameValueCollection values2 = new NameValueCollection();
            if (!IsTableStorage)
            {
                NameValueCollection values = HttpUtility.ParseQueryString(address.Query);
                foreach (string str2 in values.Keys)
                {
                    ArrayList list = new ArrayList(values.GetValues(str2));
                    list.Sort();
                    StringBuilder builder2 = new StringBuilder();
                    foreach (object obj2 in list)
                    {
                        if (builder2.Length > 0)
                        {
                            builder2.Append(",");
                        }
                        builder2.Append(obj2.ToString());
                    }
                    values2.Add((str2 == null) ? str2 : str2.ToLowerInvariant(), builder2.ToString());
                }
            }
            ArrayList list2 = new ArrayList(values2.AllKeys);
            list2.Sort();
            foreach (string str3 in list2)
            {
                StringBuilder builder3 = new StringBuilder(string.Empty);
                builder3.Append(str3);
                builder3.Append(":");
                builder3.Append(values2[str3]);
                str.Append("\n");
                str.Append(builder3.ToString());
            }
            return str.ToString();
        }

        #endregion

        #region Retry Delegate

        public delegate T RetryDelegate<T>();
        public delegate void RetryDelegate();

        const int retryCount = 3;
        const int retryIntervalMS = 200;

        // Retry delegate with default retry settings.

        public static T Retry<T>(RetryDelegate<T> del)
        {
            return Retry<T>(del, retryCount, retryIntervalMS);
        }

        // Retry delegate.

        public static T Retry<T>(RetryDelegate<T> del, int numberOfRetries, int msPause)
        {
            int counter = 0;
        RetryLabel:

            try
            {
                counter++;
                return del.Invoke();
            }
            catch (Exception ex)
            {
                if (counter > numberOfRetries)
                {
                    throw ex;
                }
                else
                {
                    if (msPause > 0)
                    {
                        Thread.Sleep(msPause);
                    }
                    goto RetryLabel;
                }
            }
        }


        // Retry delegate with default retry settings.

        public static bool Retry(RetryDelegate del)
        {
            return Retry(del, retryCount, retryIntervalMS);
        }


        public static bool Retry(RetryDelegate del, int numberOfRetries, int msPause)
        {
            int counter = 0;

        RetryLabel:
            try
            {
                counter++;
                del.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                if (counter > numberOfRetries)
                {
                    throw ex;
                }
                else
                {
                    if (msPause > 0)
                    {
                        Thread.Sleep(msPause);
                    }
                    goto RetryLabel;
                }
            }
        }

        #endregion
    }
}