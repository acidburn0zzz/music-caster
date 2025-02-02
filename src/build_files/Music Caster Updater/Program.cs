﻿// NOTE: This was the old portable updater
//  the new updater is updater.go
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.Json;

/**
old code in build.py that was used to build the updater
def get_msbuild():
    import re
    import winreg
    reg = winreg.ConnectRegistry(None, winreg.HKEY_LOCAL_MACHINE)
    root_key = winreg.OpenKey(reg, r'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
                              0, winreg.KEY_READ | winreg.KEY_WOW64_32KEY)
    num_sub_keys = winreg.QueryInfoKey(root_key)[0]
    vs = {}
    for i in range(num_sub_keys):
        with suppress(EnvironmentError):
            software: dict = {}
            software_key = winreg.EnumKey(root_key, i)
            software_key = winreg.OpenKey(root_key, software_key)
            info_key = winreg.QueryInfoKey(software_key)
            for value in range(info_key[1]):
                value = winreg.EnumValue(software_key, value)
                software[value[0]] = value[1]
            display_name = software.get('DisplayName', '')
            if re.search(r'Visual Studio (Community|Professional|Enterprise)', display_name):
                software['ver'] = int(software['DisplayName'].rsplit(maxsplit=1)[1])
                vs_ver = vs.get('ver', 0)
                if software['ver'] > vs_ver:
                    vs = software
    if vs is None: raise RuntimeWarning('No installation of Visual Studio could be found')
    ms_build_path = vs['InstallLocation'] + r'\MSBuild\Current\Bin\MSBuild.exe'
    return ms_build_path

...
ms_build = get_msbuild()
check_call(f'{ms_build} "{starting_dir}/Music Caster Updater/Music Caster Updater.sln"'
           f' /t:Build /p:Configuration=Release /p:PlatformTarget=x86')
...
# portable_files.extend([(f, os.path.basename(f)) for f in glob.iglob(f'{glob.escape(UPDATER_DIST_PATH)}/*.*')])
*/

namespace Music_Caster_Updater
{
    class Program
    {
        private static void ExtractZip(string fileName)
        {
            /**
             * Extracts fileName (ends with .zip) to root directory
             * Deletes fileName after
             */
            using (ZipArchive archive = ZipFile.OpenRead(fileName))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string dir = Path.GetDirectoryName(entry.FullName);
                    if (dir != "" && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    try
                    {
                        if (File.Exists(entry.FullName)) File.Delete(entry.FullName);
                        entry.ExtractToFile(entry.FullName);
                    }
                    catch (IOException) { }
                    catch (System.UnauthorizedAccessException) { }
                }
            }
            File.Delete(fileName);
        }
        private static void Download(string url, string outfile)
        {
            // Downloads url to outfile
            // If outfile is a zip, extract it
            Debug.WriteLine($"Downloading {outfile}");
            using WebClient myWebClient = new WebClient();
            myWebClient.DownloadFile(url, outfile);
            if (outfile.EndsWith(".zip")) ExtractZip(outfile);
        }

        private static List<string> DirectorySearch(string dir)
        {   // returns all files in a dir and its subdirs recursively
            List<string> files = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(dir)) files.Add(Path.GetFileName(f));
                foreach (string d in Directory.GetDirectories(dir)) files.AddRange(DirectorySearch(d));
            }
            catch (Exception) { }
            return files;
        }


        static void Main()
        {
            // use @ for string literals
            const string releasesURL = @"https://api.github.com/repos/elibroftw/music-caster/releases/latest";
            const string settingsFile = "settings.json";
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);  // Change working dir to dir of this program
            Dictionary<string, object> loadedSettings = new Dictionary<string, object>() { { "DEBUG", false } };

            if (File.Exists(settingsFile))
            {
                using StreamReader fs = new StreamReader(settingsFile);
                loadedSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(fs.ReadToEnd());
            }
            bool debugSetting = false;
            try
            {
                debugSetting = ((JsonElement)loadedSettings.GetValueOrDefault("DEBUG")).GetBoolean();
            }
            catch (InvalidCastException) { }


            Dictionary<string, object> jsonResponse;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(releasesURL);
            request.Method = "GET";
            request.UserAgent = "MusicCasterUpdaterC#";
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                jsonResponse = JsonSerializer.Deserialize<Dictionary<string, object>>((new StreamReader(response.GetResponseStream())).ReadToEnd());
            }

            string setupDownloadURL = "", portableDownloadURL = "";

            JsonElement assets = (JsonElement) jsonResponse.GetValueOrDefault("assets");
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                if (asset.GetProperty("name").ToString().Contains("exe"))
                    setupDownloadURL = asset.GetProperty("browser_download_url").ToString();
                else if (asset.GetProperty("name").ToString().ToLower().Contains("portable"))
                    portableDownloadURL = asset.GetProperty("browser_download_url").ToString();
            }
            if (debugSetting)
            {
                string latestVersion = jsonResponse.GetValueOrDefault("tag_name").ToString();
                Debug.WriteLine($"Latest Version: {latestVersion}");
                Debug.WriteLine($"Portable:       {portableDownloadURL}");
                Debug.WriteLine($"Installer:      {setupDownloadURL}");
            }
            else if (File.Exists("unins000.exe"))
            {   // Was installed using the Installer
                Download(setupDownloadURL, "MC_Installer.exe");
                Process.Start("MC_Installer.exe", "/VERYSILENT /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS /MERGETASKS=\"!desktopicon\"");
            }
            else
            {   // portable installation
                Download(portableDownloadURL, "Portable.zip");
                Process.Start("\"Music Caster.exe\" --nupdate");
            }
        }
    }
}
