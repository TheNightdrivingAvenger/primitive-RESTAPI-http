using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Lab5
{
    class Server
    {
        private HttpListener httpListener;
        private string rootDir;

        private HttpListenerRequest request;
        private HttpListenerResponse response;

        private const string HTMLHead = "<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><title>Response</title></head><body>";
        private const string HTMLTail = "</body></html>";

        public Server(string rootDir)
        {
            this.rootDir = rootDir;
        }

        public void Start(string prefix)
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add(prefix);
            httpListener.Start();

            while (true)
            {
                HttpListenerContext context = httpListener.GetContext();
                request = context.Request;
                response = context.Response;
                string method = context.Request.HttpMethod.ToUpper();
                switch (method)
                {
                    case "GET":
                        PerformGET(false);
                        break;
                    case "PUT":
                        PerformPUT();
                        break;
                    case "HEAD":
                        PerformGET(true);
                        break;
                    case "DELETE":
                        PerformDELETE();
                        break;
                    default:
                        NotImplemented();
                        break;
                }
            }
        }

        private void NotImplemented()
        {
            PrepareResponseHeaders(501, "Not implemented");
            response.ContentType = "text/html;charset=UTF-8";
            response.Close(StatusResponse("This method is not impelemented"), true);
        }

        private void PerformGET(bool HEAD)
        {
            string fullPath = rootDir + WebUtility.UrlDecode(request.Url.AbsolutePath);
            try
            {
                var attr = File.GetAttributes(fullPath);

                // detect whether it's a directory or file
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    response.ContentType = "text/html;charset=UTF-8";
                    if (!HEAD)
                    {
                        PrepareResponseHeaders(200, "OK");
                        response.Close(DirContentsResponse(Directory.EnumerateFileSystemEntries(fullPath)), true);
                    }
                    else
                    {
                        PrepareResponseHeaders(400, "Bad request");
                        response.Close();
                    }
                }
                else
                {
                    PrepareResponseHeaders(200, "OK");
                    response.ContentType = GetExpectedMIMEType(fullPath.Substring(fullPath.LastIndexOf('.') + 1)) + ";charset=UTF-8";
                    if (!HEAD)
                    {
                        byte[] fileContents = File.ReadAllBytes(fullPath);
                        response.Close(fileContents, true);
                    }
                    else
                    {
                        response.Close();
                    }
                }

            }
            catch (UnauthorizedAccessException)
            {
                PrepareResponseHeaders(403, "Forbidden");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("You have no rights for accessing this file/directory"), true);
            }
            catch (FileNotFoundException)
            {
                PrepareResponseHeaders(404, "Not found");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("File/directory was not found"), true);
            }
            catch (DirectoryNotFoundException)
            {
                PrepareResponseHeaders(404, "Not found");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("File/directory was not found"), true);
            }
            catch (ArgumentException)
            {
                PrepareResponseHeaders(400, "Bad request");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("Invalid path provided"), true);
            }
            catch (PathTooLongException)
            {
                PrepareResponseHeaders(414, "Path too long");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("Path too long"), true);
            }
            catch (NotSupportedException)
            {
                PrepareResponseHeaders(400, "Bad request");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("Ivalid path format"), true);
            }
            catch (IOException)
            {
                PrepareResponseHeaders(403, "Target blocked");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("File cannot be accesed due to being used by another program"), true);
            }
        }

        private void PrepareResponseHeaders(int code, string status)
        {
            response.ContentEncoding = Encoding.UTF8;
            response.StatusCode = code;
            response.StatusDescription = status;
        }

        private string GetExpectedMIMEType(string fileExtension)
        {
            if (fileExtension.Equals("txt", StringComparison.InvariantCultureIgnoreCase))
            {
                return "text/plain";
            }
            else if (fileExtension.Equals("html", StringComparison.InvariantCultureIgnoreCase))
            {
                return "text/html";
            }
            else if (fileExtension.Equals("jpeg", StringComparison.InvariantCultureIgnoreCase) ||
                fileExtension.Equals("png", StringComparison.InvariantCultureIgnoreCase) ||
                fileExtension.Equals("bmp", StringComparison.InvariantCultureIgnoreCase) ||
                fileExtension.Equals("gif", StringComparison.InvariantCultureIgnoreCase))
            {
                return "image/" + fileExtension;
            }
            else if (fileExtension.Equals("zip", StringComparison.InvariantCultureIgnoreCase) ||
                fileExtension.Equals("pdf", StringComparison.InvariantCultureIgnoreCase))
            {
                return "application/" + fileExtension;
            }
            else
            {
                return "application/octet-stream";
            }
        }

        private byte[] DirContentsResponse(IEnumerable<string> values)
        {
            string result = "";
            foreach (var value in values)
            {
                result += value.Substring(rootDir.Length) + "<br>";
            }
            return Encoding.UTF8.GetBytes(HTMLHead + result + HTMLTail);
        }

        private byte[] StatusResponse(string message)
        {
            return Encoding.UTF8.GetBytes(HTMLHead + message + HTMLTail);
        }

        private void PerformPUT()
        {
            string fullPath = rootDir + WebUtility.UrlDecode(request.Url.AbsolutePath);

            var mode = FileMode.Create;
            if (request.Headers["X-Copy-To"] != null)
            {
                mode = FileMode.Open;
            }
            FileStream newFile = null;
            FileStream copy = null;
            bool created = false;
            try
            {
                int lastSeparatorForward = fullPath.LastIndexOf('/');
                int lastSeparatorBackward = fullPath.LastIndexOf('\\');
                int lastSeparator = Math.Max(lastSeparatorForward, lastSeparatorBackward);
                Directory.CreateDirectory(fullPath.Substring(0, lastSeparator));
                created = true;
                newFile = File.Open(fullPath, mode, FileAccess.ReadWrite, FileShare.Read);

                if (request.Headers["X-Copy-To"] != null)
                {
                    string target = rootDir + WebUtility.UrlDecode(request.Headers["X-Copy-To"]);

                    lastSeparatorForward = target.LastIndexOf('/');
                    lastSeparatorBackward = target.LastIndexOf('\\');
                    lastSeparator = Math.Max(lastSeparatorForward, lastSeparatorBackward);
                    Directory.CreateDirectory(target.Substring(0, lastSeparator));

                    copy = File.Open(target, FileMode.Create, FileAccess.Write, FileShare.Read);
                    newFile.CopyTo(copy);
                    newFile.Close();
                    copy.Close();
                    PrepareResponseHeaders(200, "OK");
                    response.ContentType = "text/html;charset=UTF-8";
                    response.Close(StatusResponse("File has been copied"), true);
                    return;
                }

                if (request.HasEntityBody)
                {
                    request.InputStream.CopyTo(newFile);
                    PrepareResponseHeaders(201, "Created");
                    response.ContentType = "text/html;charset=UTF-8";
                    response.Close(StatusResponse("File has been created"), true);
                }
                else
                {
                    PrepareResponseHeaders(201, "Created");
                    response.ContentType = "text/html;charset=UTF-8";
                    response.Close(StatusResponse("An empty file has been created"), true);
                }
                newFile.Close();
            }
            catch (UnauthorizedAccessException)
            {
                PrepareResponseHeaders(403, "Forbidden");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("You have no rights for accessing this file/directory"), true);
            }
            catch (ArgumentException)
            {
                PrepareResponseHeaders(400, "Bad request");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("Invalid path provided"), true);
            }
            catch (PathTooLongException)
            {
                PrepareResponseHeaders(414, "Path too long");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("Path too long"), true);
            }
            catch (NotSupportedException)
            {
                PrepareResponseHeaders(400, "Bad request");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("Ivalid path format"), true);
            }
            catch (FileNotFoundException)
            {
                PrepareResponseHeaders(404, "Not found");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("File/directory was not found"), true);
            }
            catch (DirectoryNotFoundException)
            {
                PrepareResponseHeaders(400, "Bad request");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("Provided path is invalid"), true);
            }
            catch (IOException)
            {
                if (!created)
                {
                    PrepareResponseHeaders(400, "Bad request");
                    response.ContentType = "text/html;charset=UTF-8";
                    response.Close(StatusResponse("Specified path is a file"), true);
                }
                else
                {
                    PrepareResponseHeaders(500, "Internal server error");
                    response.ContentType = "text/html;charset=UTF-8";
                    response.Close(StatusResponse("File cannot be created/written to due to unspecified internal error"), true);
                }
            }
            finally
            {
                newFile?.Close();
                copy?.Close();
            }
        }
        
        private void PerformDELETE()
        {
            string fullPath = rootDir + WebUtility.UrlDecode(request.Url.AbsolutePath);
            // add filtering for ../ and ./?
            try
            {
                var attr = File.GetAttributes(fullPath);

                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    Directory.Delete(fullPath, true);
                    PrepareResponseHeaders(200, "OK");
                    response.ContentType = "text/html;charset=UTF-8";
                    response.Close(StatusResponse("Directory with all contents has been deleted"), true);
                }
                else
                {
                    // check because no exception is thrown by File.Delete on non-existent file
                    if (!File.Exists(fullPath))
                    {
                        PrepareResponseHeaders(404, "Not found");
                        response.ContentType = "text/html;charset=UTF-8";
                        response.Close(StatusResponse("The requested file has not been found"), true);
                        return;
                    }

                    File.Delete(fullPath);

                    PrepareResponseHeaders(200, "OK");
                    response.ContentType = "text/html;charset=UTF-8";
                    response.Close(StatusResponse("File has been deleted"), true);
                }
            }
            catch (UnauthorizedAccessException)
            {
                PrepareResponseHeaders(403, "Forbidden");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("You have no rights for accessing this file/directory, or file cannot be accessed" +
                    ", or the file is a read-only file"), true);
            }
            catch (ArgumentException)
            {
                PrepareResponseHeaders(400, "Bad request");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("Invalid path provided"), true);
            }
            catch (PathTooLongException)
            {
                PrepareResponseHeaders(414, "Path too long");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("Path too long"), true);
            }
            catch (NotSupportedException)
            {
                PrepareResponseHeaders(400, "Bad request");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("Ivalid path format"), true);
            }
            catch (FileNotFoundException)
            {
                PrepareResponseHeaders(404, "Not found");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("File/directory was not found"), true);
            }
            catch (DirectoryNotFoundException)
            {
                PrepareResponseHeaders(400, "Bad request");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("Provided path is invalid"), true);
            }
            catch (IOException)
            {
                PrepareResponseHeaders(403, "Target blocked");
                response.ContentType = "text/html;charset=UTF-8";
                response.Close(StatusResponse("File cannot be accesed due to being used by another program"), true);
            }
        }
    }
}
