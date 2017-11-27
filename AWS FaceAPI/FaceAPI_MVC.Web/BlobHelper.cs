using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace FaceAPI_MVC.Web
{
    public class BlobHelper : RESTHelper
    {
        // Constructor.

        public BlobHelper(string storageAccount, string storageKey) : base("http://" + storageAccount + ".blob.core.windows.net/", storageAccount, storageKey)
        {
        }


        // List containers.
        // Return true on success, false if not found, throw exception on error.

        public List<string> ListContainers()
        {
            return Retry<List<string>>(delegate ()
            {
                HttpWebResponse response;

                List<string> containers = new List<string>();

                try
                {
                    response = CreateRESTRequest("GET", "?comp=list").GetResponse() as HttpWebResponse;

                    if ((int)response.StatusCode == 200)
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            string result = reader.ReadToEnd();

                            XElement x = XElement.Parse(result);
                            foreach (XElement container in x.Element("Containers").Elements("Container"))
                            {
                                containers.Add(container.Element("Name").Value);
                            }
                        }
                    }

                    response.Close();

                    return containers;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return null;

                    throw;
                }
            });
        }


        // Create a blob container. 
        // Return true on success, false if already exists, throw exception on error.

        public bool CreateContainer(string container)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    response = CreateRESTRequest("PUT", container + "?restype=container").GetResponse() as HttpWebResponse;
                    response.Close();
                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 409)
                        return false;

                    throw;
                }
            });
        }


        // Get container properties.
        // Return true on success, false if not found, throw exception on error.
        // TODO: modify for retries.

        public bool GetContainerProperties(string container, out string eTag, out string lastModified)
        {
            HttpWebResponse response;

            eTag = String.Empty;
            lastModified = String.Empty;

            try
            {
                response = CreateRESTRequest("HEAD", container + "?restype=container").GetResponse() as HttpWebResponse;
                response.Close();

                if ((int)response.StatusCode == 200)
                {
                    if (response.Headers != null)
                    {
                        eTag = response.Headers["ETag"];
                        lastModified = response.Headers["LastModifiedUtc"];
                    }
                }

                return true;
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError &&
                    ex.Response != null &&
                    (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                    return false;

                throw;
            }
        }


        // Get container metadata.
        // Return true on success, false if not found, throw exception on error.

        public SortedList<string, string> GetContainerMetadata(string container)
        {
            return Retry<SortedList<string, string>>(delegate ()
            {
                HttpWebResponse response;

                SortedList<string, string> metadataList = new SortedList<string, string>();

                try
                {
                    response = CreateRESTRequest("HEAD", container + "?restype=container&comp=metadata", string.Empty, metadataList).GetResponse() as HttpWebResponse;
                    response.Close();

                    if ((int)response.StatusCode == 200)
                    {
                        if (response.Headers != null)
                        {
                            for (int i = 0; i < response.Headers.Count; i++)
                            {
                                if (response.Headers.Keys[i].StartsWith("x-ms-meta-"))
                                {
                                    metadataList.Add(response.Headers.Keys[i], response.Headers[i]);
                                }
                            }
                        }
                    }

                    return metadataList;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return null;

                    throw;
                }
            });
        }


        // Set container metadata.
        // Return true on success, false if not found, throw exception on error.

        public bool SetContainerMetadata(string container, SortedList<string, string> metadataList)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();

                    if (metadataList != null)
                    {
                        foreach (KeyValuePair<string, string> value in metadataList)
                        {
                            headers.Add("x-ms-meta-" + value.Key, value.Value);
                        }
                    }

                    response = CreateRESTRequest("PUT", container + "?restype=container&comp=metadata", string.Empty, headers).GetResponse() as HttpWebResponse;
                    response.Close();

                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return false;

                    throw;
                }
            });
        }


        // Get container access control.
        // Return true on success, false if not found, throw exception on error.
        // accessLevel set to container|blob|private.

        public string GetContainerACL(string container)
        {
            return Retry<string>(delegate ()
            {
                HttpWebResponse response;

                string accessLevel = String.Empty;

                try
                {
                    response = CreateRESTRequest("GET", container + "?restype=container&comp=acl").GetResponse() as HttpWebResponse;
                    response.Close();

                    if ((int)response.StatusCode == 200)
                    {
                        if (response.Headers != null)
                        {
                            string access = response.Headers["x-ms-blob-public-access"];
                            if (access != null)
                            {
                                switch (access)
                                {
                                    case "container":
                                    case "blob":
                                        accessLevel = access;
                                        break;
                                    case "true":
                                        accessLevel = "container";
                                        break;
                                    default:
                                        accessLevel = "private";
                                        break;
                                }
                            }
                            else
                            {
                                accessLevel = "private";
                            }
                        }
                    }

                    return accessLevel;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return null;

                    throw;
                }
            });
        }


        // Set container access control.
        // Return true on success, false if not found, throw exception on error. 
        // Set accessLevel to container|blob|private.

        public bool SetContainerACL(string container, string accessLevel)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();
                    switch (accessLevel)
                    {
                        case "container":
                        case "blob":
                            headers.Add("x-ms-blob-public-access", accessLevel);
                            break;
                        case "private":
                        default:
                            break;
                    }

                    response = CreateRESTRequest("PUT", container + "?restype=container&comp=acl", string.Empty, headers).GetResponse() as HttpWebResponse;
                    response.Close();

                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return false;

                    throw;
                }
            });
        }


        // Get container access policy.
        // Return true on success, false if not found, throw exception on error. 

        public string GetContainerAccessPolicy(string container)
        {
            return Retry<string>(delegate ()
            {
                HttpWebResponse response;

                string accessPolicyXml = String.Empty;

                try
                {
                    response = CreateRESTRequest("GET", container + "?restype=container&comp=acl").GetResponse() as HttpWebResponse;

                    if ((int)response.StatusCode == 200)
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            accessPolicyXml = reader.ReadToEnd();
                        }
                    }

                    response.Close();

                    return accessPolicyXml;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return null;

                    throw;
                }
            });
        }


        // Set container access policy (container|blob|private).
        // Return true on success, false if not found, throw exception on error.

        public bool SetContainerAccessPolicy(string container, string accessLevel, string accessPolicyXml)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();
                    switch (accessLevel)
                    {
                        case "container":
                        case "blob":
                            headers.Add("x-ms-blob-public-access", accessLevel);
                            break;
                        case "private":
                        default:
                            break;
                    }

                    response = CreateRESTRequest("PUT", container + "?restype=container&comp=acl", accessPolicyXml, headers).GetResponse() as HttpWebResponse;
                    response.Close();

                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return false;

                    throw;
                }
            });
        }


        // Delete a blob container. 
        // Return true on success, false if not found, throw exception on error.

        public bool DeleteContainer(string container)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    response = CreateRESTRequest("DELETE", container).GetResponse() as HttpWebResponse;
                    response.Close();
                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return false;

                    throw;
                }
            });
        }


        // List blobs in a container.
        // Return true on success, false if not found, throw exception on error.

        public List<string> ListBlobs(string container)
        {
            return Retry<List<string>>(delegate ()
            {
                HttpWebResponse response;

                List<string> blobs = new List<string>();

                try
                {
                    response = CreateRESTRequest("GET", container + "?restype=container&comp=list&include=snapshots&include=metadata").GetResponse() as HttpWebResponse;

                    if ((int)response.StatusCode == 200)
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            string result = reader.ReadToEnd();

                            XElement x = XElement.Parse(result);
                            foreach (XElement blob in x.Element("Blobs").Elements("Blob"))
                            {
                                blobs.Add(blob.Element("Name").Value);
                            }
                        }
                    }

                    response.Close();

                    return blobs;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return null;

                    throw;
                }
            });
        }


        // Retrieve the content of a blob. 
        // Return true on success, false if not found, throw exception on error.

        public string GetBlob(string container, string blob)
        {
            return Retry<string>(delegate ()
            {
                HttpWebResponse response;

                string content = null;

                try
                {
                    response = CreateRESTRequest("GET", container + "/" + blob).GetResponse() as HttpWebResponse;

                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        content = reader.ReadToEnd();
                    }

                    response.Close();
                    return content;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 409)
                        return null;

                    throw;
                }
            });
        }


        // Create or update a blob. 
        // Return true on success, false if not found, throw exception on error.

        public bool PutBlob(string container, string blob, string content)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();
                    headers.Add("x-ms-blob-type", "BlockBlob");

                    response = CreateRESTRequest("PUT", container + "/" + blob, content, headers).GetResponse() as HttpWebResponse;
                    response.Close();
                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 409)
                        return false;

                    throw;
                }
            });
        }


        // Create or update a page blob. 
        // Return true on success, false if not found, throw exception on error.

        public bool PutBlob(string container, string blob, int pageBlobSize)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();
                    headers.Add("x-ms-blob-type", "PageBlob");
                    headers.Add("x-ms-blob-content-length", pageBlobSize.ToString());

                    response = CreateRESTRequest("PUT", container + "/" + blob, null, headers).GetResponse() as HttpWebResponse;
                    response.Close();
                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 409)
                        return false;

                    throw;
                }
            });
        }


        // Create or update a blob condition based on an expected ETag value.
        // Return true on success, false if not found, throw exception on error.

        public bool PutBlobIfUnchanged(string container, string blob, string content, string expectedETagValue)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();
                    headers.Add("x-ms-blob-type", "BlockBlob");
                    headers.Add("If-Match", expectedETagValue);

                    response = CreateRESTRequest("PUT", container + "/" + blob, content, headers, expectedETagValue).GetResponse() as HttpWebResponse;
                    response.Close();
                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        ((int)(ex.Response as HttpWebResponse).StatusCode == 409 ||
                        (int)(ex.Response as HttpWebResponse).StatusCode == 412))
                        return false;

                    throw;
                }
            });
        }


        // Create or update a blob with an MD5 hash.
        // Return true on success, false if not found, throw exception on error.

        public bool PutBlobWithMD5(string container, string blob, string content)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    string md5 = Convert.ToBase64String(new System.Security.Cryptography.MD5CryptoServiceProvider().ComputeHash(System.Text.Encoding.Default.GetBytes(content)));

                    SortedList<string, string> headers = new SortedList<string, string>();
                    headers.Add("x-ms-blob-type", "BlockBlob");
                    headers.Add("Content-MD5", md5);

                    response = CreateRESTRequest("PUT", container + "/" + blob, content, headers, String.Empty, md5).GetResponse() as HttpWebResponse;
                    response.Close();
                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        ((int)(ex.Response as HttpWebResponse).StatusCode == 409 ||
                        (int)(ex.Response as HttpWebResponse).StatusCode == 400))
                        return false;

                    throw;
                }
            });
        }


        // Retrieve a page from a block blob. 
        // Return true on success, false if not found, throw exception on error.

        public string GetPage(string container, string blob, int pageOffset, int pageSize)
        {
            return Retry<string>(delegate ()
            {
                HttpWebResponse response;

                string content = null;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();
                    headers.Add("x-ms-range", "bytes=" + pageOffset.ToString() + "-" + (pageOffset + pageSize - 1).ToString());
                    response = CreateRESTRequest("GET", container + "/" + blob, null, headers).GetResponse() as HttpWebResponse;

                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        content = reader.ReadToEnd();
                    }

                    response.Close();
                    return content;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 409)
                        return null;

                    throw;
                }
            });
        }


        // Write a page to a page blob.
        // Return true on success, false if not found, throw exception on error.

        public bool PutPage(string container, string blob, string content, int pageOffset, int pageSize)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();
                    headers.Add("x-ms-page-write", "update");
                    headers.Add("x-ms-range", "bytes=" + pageOffset.ToString() + "-" + (pageOffset + pageSize - 1).ToString());

                    response = CreateRESTRequest("PUT", container + "/" + blob + "?comp=page ", content, headers).GetResponse() as HttpWebResponse;
                    response.Close();
                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 409)
                        return false;

                    throw;
                }
            });
        }


        // Retrieve the list of regions in use for a page blob. 
        // Return true on success, false if not found, throw exception on error.

        public string[] GetPageRegions(string container, string blob)
        {
            return Retry<string[]>(delegate ()
            {
                HttpWebResponse response;

                string[] regions = null;

                try
                {
                    response = CreateRESTRequest("GET", container + "/" + blob + "?comp=pagelist").GetResponse() as HttpWebResponse;

                    if ((int)response.StatusCode == 200)
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            List<string> regionList = new List<string>();
                            string result = reader.ReadToEnd();

                            XElement xml = XElement.Parse(result);

                            foreach (XElement range in xml.Elements("PageRange"))
                            {
                                regionList.Add(range.ToString());
                            }

                            regions = new string[regionList.Count];

                            int i = 0;
                            foreach (string region in regionList)
                            {
                                regions[i++] = region;
                            }
                        }
                    }

                    response.Close();
                    return regions;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 409)
                        return null;

                    throw;
                }
            });
        }


        // Copy a blob. 
        // Return true on success, false if not found, throw exception on error.

        public bool CopyBlob(string sourceContainer, string sourceBlob, string destContainer, string destBlob)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();
                    headers.Add("x-ms-copy-source", "/" + StorageAccount + "/" + sourceContainer + "/" + sourceBlob);

                    response = CreateRESTRequest("PUT", destContainer + "/" + destBlob, null, headers).GetResponse() as HttpWebResponse;
                    response.Close();
                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return false;

                    throw;
                }
            });
        }


        // Retrieve the list of uploaded blocks for a blob. 
        // Return true on success, false if not found, throw exception on error.

        public string[] GetBlockList(string container, string blob)
        {
            return Retry<string[]>(delegate ()
            {
                HttpWebResponse response;

                string[] blockIds = null;

                try
                {
                    response = CreateRESTRequest("GET", container + "/" + blob + "?comp=blocklist").GetResponse() as HttpWebResponse;

                    if ((int)response.StatusCode == 200)
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            List<string> blockIdList = new List<string>();
                            string result = reader.ReadToEnd();

                            XElement xml = XElement.Parse(result);

                            foreach (XElement blockGroup in xml.Elements())
                            {
                                foreach (XElement block in blockGroup.Elements("Block"))
                                {
                                    blockIdList.Add(block.Element("Name").Value);
                                }
                            }

                            blockIds = new string[blockIdList.Count];

                            int i = 0;
                            foreach (string blockId in blockIdList)
                            {
                                blockIds[i++] = blockId;
                            }
                        }
                    }

                    response.Close();
                    return blockIds;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 409)
                        return null;

                    throw;
                }
            });
        }


        // Put block - upload a block (portion) of a blob. 
        // Return true on success, false if not found, throw exception on error.

        public bool PutBlock(string container, string blob, int blockId, string[] blockIds, string content)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();

                    byte[] blockIdBytes = BitConverter.GetBytes(blockId);
                    string blockIdBase64 = Convert.ToBase64String(blockIdBytes);

                    blockIds[blockId] = blockIdBase64;

                    response = CreateRESTRequest("PUT", container + "/" + blob + "?comp=block&blockid=" + blockIdBase64, content, headers).GetResponse() as HttpWebResponse;
                    response.Close();
                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 409)
                        return false;

                    throw;
                }
            });
        }


        // Put block list - complete creation of blob based on uploaded content.
        // Return true on success, false if not found, throw exception on error.

        public bool PutBlockList(string container, string blob, string[] blockIds)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    StringBuilder content = new StringBuilder();
                    content.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                    content.Append("<BlockList>");
                    for (int i = 0; i < blockIds.Length; i++)
                    {
                        content.Append("<Latest>" + blockIds[i] + "</Latest>");
                    }
                    content.Append("</BlockList>");

                    response = CreateRESTRequest("PUT", container + "/" + blob + "?comp=blocklist", content.ToString(), null).GetResponse() as HttpWebResponse;
                    response.Close();
                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 409)
                        return false;

                    throw;
                }
            });
        }


        // Create a snapshot of a blob. 
        // Return true on success, false if not found, throw exception on error.

        public bool SnapshotBlob(string container, string blob)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    response = CreateRESTRequest("PUT", container + "/" + blob + "?comp=snapshot").GetResponse() as HttpWebResponse;
                    response.Close();
                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return false;

                    throw;
                }
            });
        }


        // Delete a blob. 
        // Return true on success, false if not found, throw exception on error.

        public bool DeleteBlob(string container, string blob)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    response = CreateRESTRequest("DELETE", container + "/" + blob).GetResponse() as HttpWebResponse;
                    response.Close();
                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return false;

                    throw;
                }
            });
        }


        // Lease a blob.
        // Lease action: acquire|renew|break|release.
        // Lease Id: returned on acquire action; must be specified for all other actions.
        // Return true on success, false if not found, throw exception on error.

        public string LeaseBlob(string container, string blob, string leaseAction, string leaseId)
        {
            return Retry<string>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();
                    headers.Add("x-ms-lease-action", leaseAction);
                    if (!String.IsNullOrEmpty(leaseId))
                    {
                        headers.Add("x-ms-lease-id", leaseId);
                    }

                    response = CreateRESTRequest("PUT", container + "/" + blob + "?comp=lease", null, headers).GetResponse() as HttpWebResponse;
                    response.Close();

                    if (leaseAction == "acquire")
                    {
                        leaseId = response.Headers["x-ms-lease-id"];
                    }

                    return leaseId;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 409)
                        return null;

                    throw;
                }
            });
        }


        // Retrieve a blob's properties.
        // Return true on success, false if not found, throw exception on error.

        public SortedList<string, string> GetBlobProperties(string container, string blob)
        {
            return Retry<SortedList<string, string>>(delegate ()
            {
                HttpWebResponse response;

                SortedList<string, string> propertiesList = new SortedList<string, string>();

                try
                {
                    response = CreateRESTRequest("HEAD", container + "/" + blob).GetResponse() as HttpWebResponse;
                    response.Close();

                    if ((int)response.StatusCode == 200)
                    {
                        if (response.Headers != null)
                        {
                            for (int i = 0; i < response.Headers.Count; i++)
                            {
                                propertiesList.Add(response.Headers.Keys[i], response.Headers[i]);
                            }
                        }
                    }

                    return propertiesList;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return null;

                    throw;
                }
            });
        }


        // Set blob properties.
        // Return true on success, false if not found, throw exception on error.

        public bool SetBlobProperties(string container, string blob, SortedList<string, string> propertyList)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();

                    if (propertyList != null)
                    {
                        foreach (KeyValuePair<string, string> value in propertyList)
                        {
                            headers.Add(value.Key, value.Value);
                        }
                    }

                    response = CreateRESTRequest("PUT", container + "/" + blob + "?comp=properties", string.Empty, headers).GetResponse() as HttpWebResponse;
                    response.Close();

                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return false;

                    throw;
                }
            });
        }


        // Retrieve a blob's metadata.
        // Return true on success, false if not found, throw exception on error.

        public SortedList<string, string> GetBlobMetadata(string container, string blob)
        {
            return Retry<SortedList<string, string>>(delegate ()
            {
                HttpWebResponse response;

                SortedList<string, string> metadata = new SortedList<string, string>();

                try
                {
                    response = CreateRESTRequest("HEAD", container + "/" + blob + "?comp=metadata").GetResponse() as HttpWebResponse;
                    response.Close();

                    if ((int)response.StatusCode == 200)
                    {
                        if (response.Headers != null)
                        {
                            for (int i = 0; i < response.Headers.Count; i++)
                            {
                                if (response.Headers.Keys[i].StartsWith("x-ms-meta-"))
                                {
                                    metadata.Add(response.Headers.Keys[i], response.Headers[i]);
                                }
                            }
                        }
                    }

                    return metadata;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return null;

                    throw;
                }
            });
        }


        // Set blob metadata.
        // Return true on success, false if not found, throw exception on error.

        public bool SetBlobMetadata(string container, string blob, SortedList<string, string> metadataList)
        {
            return Retry<bool>(delegate ()
            {
                HttpWebResponse response;

                try
                {
                    SortedList<string, string> headers = new SortedList<string, string>();

                    if (metadataList != null)
                    {
                        foreach (KeyValuePair<string, string> value in metadataList)
                        {
                            headers.Add("x-ms-meta-" + value.Key, value.Value);
                        }
                    }

                    response = CreateRESTRequest("PUT", container + "/" + blob + "?comp=metadata", string.Empty, headers).GetResponse() as HttpWebResponse;
                    response.Close();

                    return true;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError &&
                        ex.Response != null &&
                        (int)(ex.Response as HttpWebResponse).StatusCode == 404)
                        return false;

                    throw;
                }
            });
        }

    }
}