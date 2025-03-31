using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Pulumi;
using Pulumi.Gcp.Storage;
using Pulumi.Gcp.Storage.Inputs;

return await Deployment.RunAsync(() =>
{
    // the main directory for deploying the sites
    string primaryDirectory = "./websites";
    var websites = Directory.GetDirectories(primaryDirectory);

    // Buckets for each website
    var siteBuckets = new Dictionary<string, Output<string>>();

    // The endpoints for the sites
    var siteEndpoints = new Dictionary<string, Output<string>>();

    foreach (var site in websites)
    {
        var siteName = Path.GetFileName(site);

        // Create a GCP resource (Storage Bucket)
        var bucket = new Bucket($"bucket-{siteName}", new BucketArgs
        {
            Location = "US",
            Website = new BucketWebsiteArgs
            {
                MainPageSuffix = "index.html"
            },
            UniformBucketLevelAccess = true
        });

        // Upload all files and subdirectories inside the site folder
        var files = Directory.EnumerateFiles(site, "*", SearchOption.AllDirectories);

        string? indexHtmlHashedName = null;
        var cacheBustedFiles = new Dictionary<string, string>();
        var directories = new HashSet<string>();

        // JS and CSS files must be cache-busted, the easiest way to do that is to add a hash, and then replace the js and css files with the hash file names
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(site, file).Replace("\\", "/");
            var objectName = relativePath; // Default to using the relative path

            // Each top-level directory represents a file pathway from the index.html
            var topLevelDirs = Directory.GetDirectories(site)
                .Select(d => Path.GetFileName(d)) 
                .Where(d => !string.IsNullOrEmpty(d)); 

            foreach (var dir in topLevelDirs)
            {
                directories.Add(dir);
            }

            // If it's CSS or JS, generate a cache-busting name
            if (Path.GetExtension(file).Equals(".css", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(file).Equals(".js", StringComparison.OrdinalIgnoreCase))
            {
                var hash = ComputeFileHash(file);
                var dirName = Path.GetDirectoryName(relativePath)?.Replace("\\", "/") ?? "";
                objectName = string.IsNullOrEmpty(dirName)
                    ? $"{Path.GetFileNameWithoutExtension(file)}-{hash}{Path.GetExtension(file)}"
                    : $"{dirName}/{Path.GetFileNameWithoutExtension(file)}-{hash}{Path.GetExtension(file)}";
            }

            cacheBustedFiles[relativePath] = objectName;

            // Add the bucket object, the Name.Apply must be used here because bucket.Name is a future Output<T>
            _ = bucket.Name.Apply(bucketName =>
            {
                var resourceName = $"bucket-object-{bucketName}-{relativePath.Replace("/", "-").Replace(".", "-")}";
                
                return new BucketObject(resourceName, new BucketObjectArgs
                {
                    Bucket = bucketName,
                    Name = objectName,
                    Source = new FileAsset(file),
                    CacheControl = "public, max-age=0, no-cache, no-store, must-revalidate",
                    ContentType = GetContentType(file)
                });
            });


        }

        // Since we are using autonaming which results in cache busting we need to update the file names of our JS and CSS files
        // ONLY find index.html - could be expanded to all files that end in .html
        var indexHtmlPath = files.FirstOrDefault(f => Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase));
        if(indexHtmlPath != null){
            var htmlContent = File.ReadAllText(indexHtmlPath);
            foreach (var (original, newName) in cacheBustedFiles)
            {
                // Only replace if the bucket object name didn't change
                if(original != newName){
                    var originalFileName = Path.GetFileName(original); 
                    var newFileName = Path.GetFileName(newName);

                    // Replace only the file name within paths
                    htmlContent = htmlContent.Replace($"{originalFileName}", $"{newFileName}");
                }

            }

            // For each directory that was identified earlier we want to replace any absolute files/folder paths with a local file pre-fix
            foreach(var directory in directories){
                // Ensure we're only modifying absolute paths
                htmlContent = Regex.Replace(htmlContent, $@"(?<=['""\s])/{Regex.Escape(directory)}(?=[/'""\s])", $"./{directory}");
            }

            // the file needs to be saved temporarily before we can write it as index
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.html");
            File.WriteAllText(tempPath, htmlContent);

            // Generate cache-busted name for index.html itself
            var hash = ComputeFileHash(tempPath);
            indexHtmlHashedName = $"index-{hash}.html"; // Fix: No extra subdirectory

            _ = new BucketObject(indexHtmlHashedName, new BucketObjectArgs
            {
                Bucket = bucket.Name,
                Name = indexHtmlHashedName,
                Source = new FileAsset(tempPath),
                CacheControl = "public, max-age=0, no-cache, no-store, must-revalidate"
            });

        }

        _ = new BucketIAMBinding($"binding-{siteName}", new BucketIAMBindingArgs
        {
            Bucket = bucket.Name,
            Role = "roles/storage.objectViewer",
            Members = new[] { "allUsers" },
        });



        // Store the bucket's website URL
        siteBuckets[siteName] = bucket.Url;

        // Create an endpoint using the hashed index.html filename
        if (indexHtmlHashedName != null)
        {
            siteEndpoints[siteName] = Output.Format($"http://storage.googleapis.com/{bucket.Name}/{indexHtmlHashedName}");
        }

    }
    // Export the DNS name of the bucket
    return new Dictionary<string, object?>
    {
        ["bucketName"] = siteBuckets,
        ["siteEndpoints"] = siteEndpoints
    };
});

// Compute file hash for cache-busting
string ComputeFileHash(string filePath)
{
    using (var stream = File.OpenRead(filePath))
    using (var sha256 = SHA256.Create())
    {
        var hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8); // First 8 chars of hash
    }
}

string GetContentType(string filePath)
{
    var ext = Path.GetExtension(filePath).ToLower();
    return ext switch
    {
        ".html" => "text/html",
        ".css" => "text/css",
        ".js" => "application/javascript",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".ttf" => "font/ttf",
        ".eot" => "application/vnd.ms-fontobject",
        ".json" => "application/json",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream"
    };
}