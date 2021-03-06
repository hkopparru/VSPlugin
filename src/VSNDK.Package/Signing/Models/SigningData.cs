﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows.Data;
using System.IO;
using System.IO.Packaging;
using Microsoft.Win32;

namespace RIM.VSNDK_Package.Signing.Models
{
    /// <summary>
    /// Common Class for the signing dialogs
    /// </summary>
    public class SigningData : INotifyPropertyChanged
    {
        /// <summary>
        /// Constants
        /// </summary>
        private const string _colRegistered = "Registered";
        private const string _colUnregistered = "Unregistered";
        private const string _colUnregisterdText = "UnregisteredText";
        public const string _p12 = @"/author.p12";
        public const string _tokencsk = @"/bbidtoken.csk";
        public const string _db = @"/barsigner.db";
        public const string _signercsk = @"/bbsigner.csk";
        public const string _bbt_id_rsa = @"/bb_id_rsa";
        public const string _bbt_id_rsa_pub = @"/bb_id_rsa.pub";

        /// <summary>
        /// Private Members
        /// </summary>
        private bool _registered;
        private string _certPath;
        private string _errors;
        private string _message;

        /// <summary>
        /// Constructor
        /// </summary>
        public SigningData()
        {
            _certPath = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Research In Motion\";

            RefreshScreen();
        }

        /// <summary>
        /// Refresh the screen data
        /// </summary>
        public void RefreshScreen()
        {
            Registered = File.Exists(_certPath + _p12); 
        }

        /// <summary>
        /// Unregistered propert 
        /// For setting the enabled property for the Unregistered button
        /// </summary>
        public bool Unregistered
        {
            get
            {
                return !_registered;
            }
        }

        /// <summary>
        /// Returns the path to the certificate file.
        /// </summary>
        public string CertPath
        {
            get
            {
                return _certPath;
            }
            set
            {
                _certPath = value;
            }
        }

        /// <summary>
        /// Returns the path to the bbidtoken path.
        /// </summary>
        public string bbidtokenPath
        {
            get
            {
                return _certPath + _tokencsk;
            }
        }

        /// <summary>
        /// Property for setting the registered label
        /// </summary>
        public string UnregisteredText
        {
            get
            {
                return _registered ? "Registered" : "Not Registered";
            }
        }

        /// <summary>
        /// Property for getting/setting _errors
        /// </summary>
        public string Errors
        {
            get
            {
                return _errors;
            }
            set
            {
                _errors = value;
            }
        }

        /// <summary>
        /// Property for getting/setting _message
        /// </summary>
        public string Messages
        {
            get
            {
                return _message;
            }
            set
            {
                _message = value;
            }
        }

        /// <summary>
        /// Function to backup the signing certs at the specified path
        /// </summary>
        /// <param name="certPath">Path to the signing keys</param>
        /// <param name="toZipFile">Path to destination zip file</param>
        public void Backup(string toZipFile)
        {
            using (Package pkg = Package.Open(CertPath + toZipFile, FileMode.Create))
            {
                AddUriToPackage(_p12, pkg);
                AddUriToPackage(_tokencsk, pkg);
                AddUriToPackage(_db, pkg);
                AddUriToPackage(_signercsk, pkg);
                AddUriToPackage(_bbt_id_rsa, pkg);
                AddUriToPackage(_bbt_id_rsa_pub, pkg);
            }
        }

        /// <summary>
        /// Set Password into the registry.
        /// </summary>
        private void setPassword(string password)
        {
            RegistryKey rkHKCU = Registry.CurrentUser;
            RegistryKey rkCDKPass = null;

            rkCDKPass = rkHKCU.CreateSubKey("Software\\BlackBerry\\BlackBerryVSPlugin");
            rkCDKPass.SetValue("CSKPass", GlobalFunctions.Encrypt(password));

            rkCDKPass.Close();
            rkHKCU.Close();
        }

         /// <summary>
        /// Run the blackberry-signer tool with parameters passed in
        /// </summary>
        /// <returns></returns>
        public bool Register(string authorID, string password)
        {
            bool success = false;
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = p.StartInfo;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            p.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler(ErrorDataReceived);
            p.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(OutputDataReceived);

            //run register tool
            startInfo.FileName = "cmd.exe";
            startInfo.WorkingDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\BlackBerry\\VSPlugin-NDK\\qnxtools\\bin\\";
            startInfo.Arguments = string.Format("/C blackberry-keytool -genkeypair -storepass {0} -author {1}", password, "\"" + authorID + "\"");

            try
            {
                p.Start();
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
                p.WaitForExit();
                if (p.ExitCode != 0)
                    success = false;
                else
                    success = true;
                p.Close();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(startInfo.Arguments);
                System.Diagnostics.Debug.WriteLine(e.Message);
                success = false;
            }

            setPassword(password);

            return success && string.IsNullOrEmpty(_errors);
        }

        /// <summary>
        /// Run the blackberry-signer tool with parameters passed in
        /// </summary>
        /// <returns></returns>
        public bool UnRegister()
        {
            bool success = false;

            try
            {
      
                System.Diagnostics.Process p = new System.Diagnostics.Process();
                System.Diagnostics.ProcessStartInfo startInfo = p.StartInfo;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardOutput = true;
                p.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler(ErrorDataReceived);
                p.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(OutputDataReceived);

                //run register tool
                startInfo.FileName = "cmd.exe";
                startInfo.WorkingDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\BlackBerry\\VSPlugin-NDK\\qnxtools\\bin\\";
                startInfo.Arguments = string.Format("/C blackberry-signer.bat -cskdelete");

                p.Start();
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    success = false;
                }
                else
                {
                    /// Remove Files
                    FileInfo fi_p12 = new FileInfo(System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Research In Motion\author.p12");
                    FileInfo fi_csk = new FileInfo(System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Research In Motion\bbidtoken.csk");
                    fi_p12.Delete();
                    fi_csk.Delete();
                    /// Set password to blank
                    setPassword("");

                    success = true;
                }
                p.Close();
            }
            catch (Exception e)
            {
                _errors += e.Message + "\n";
                success = false;
            }

            return success;
        }


        /// <summary>
        /// Event Handler for output received from the Register Process
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OutputDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                if (e.Data.Contains("Error:"))
                    _errors += e.Data + "\n";
                else
                    _message += e.Data + "\n";
            }
        }

        /// <summary>
        /// Event Handler for the error data received from the Registger Process
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ErrorDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                _errors += e.Data + "\n";
            }
        }

        /// <summary>
        /// Function to clean up after register process
        /// </summary>
        public void CleanUp()
        {
            if (!File.Exists(System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Research In Motion\author.p12"))
            {
                FileInfo fi_csk = new FileInfo(System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Research In Motion\bbidtoken.csk");

                try
                {
                    fi_csk.Delete();
                }
                catch (System.IO.IOException ex)
                {

                }
            }
        }

        /// <summary>
        /// Function to add a file to the zip
        /// </summary>
        /// <param name="file">File name</param>
        /// <param name="pkg">Package to add file to</param>
        private void AddUriToPackage(string file, Package pkg)
        {
            if (File.Exists(_certPath + file))
            {
                Uri uri = null;
                PackagePart pkgPart = null;
                uri = PackUriHelper.CreatePartUri(new Uri(file, UriKind.Relative));
                pkgPart = pkg.CreatePart(uri, string.Empty);
                using (FileStream fileStream = new FileStream(_certPath + file, FileMode.Open, FileAccess.Read))
                {
                    CopyStream(fileStream, pkgPart.GetStream());
                }
            }
        }

        /// <summary>
        /// Function to unzip and restore a set of singing keys
        /// </summary>
        /// <param name="fromZipFile"></param>
        /// <param name="certPath"></param>
        public void Restore(string fromZipFile)
        {

            Package zipFilePackage = ZipPackage.Open(fromZipFile, FileMode.Open, FileAccess.ReadWrite);

            //Iterate through the all the files that 
            //is added within the collection and 
            foreach (ZipPackagePart contentFile in zipFilePackage.GetParts())
            {
                createFile(contentFile);
            }

            zipFilePackage.Close();

        }


        /// <summary>
        /// Method to create file at the temp folder
        /// </summary>
        /// <param name="rootFolder"></param>
        /// <param name="contentFileURI"></param>
        /// <returns></returns>
        private void createFile(ZipPackagePart contentFile)
        {
            // Initially create file under the folder specified
            string contentFilePath = string.Empty;
            contentFilePath = contentFile.Uri.OriginalString.Replace('/',
                             System.IO.Path.DirectorySeparatorChar);

            if (contentFilePath.StartsWith(
                System.IO.Path.DirectorySeparatorChar.ToString()))
            {
                contentFilePath = contentFilePath.TrimStart(
                                         System.IO.Path.DirectorySeparatorChar);
            }
            else
            {
                //do nothing
            }

            contentFilePath = System.IO.Path.Combine(_certPath, contentFilePath);

            //Check for the folder already exists. If not then create that folder

            if (System.IO.Directory.Exists(
                System.IO.Path.GetDirectoryName(contentFilePath)) != true)
            {
                System.IO.Directory.CreateDirectory(
                          System.IO.Path.GetDirectoryName(contentFilePath));
            }
            else
            {
                //do nothing
            }

            System.IO.FileStream newFileStream =
                    System.IO.File.Create(contentFilePath);
            newFileStream.Close();
            byte[] content = new byte[contentFile.GetStream().Length];
            contentFile.GetStream().Read(content, 0, content.Length);
            System.IO.File.WriteAllBytes(contentFilePath, content);

        }

        /// <summary>
        /// Function to copy a stream from one stream to another
        /// </summary>
        /// <param name="source">Source Stream</param>
        /// <param name="target">Target Stream</param>
        private void CopyStream(Stream source, Stream target)
        {
            const int bufSize = 0x1000;
            byte[] buf = new byte[bufSize];
            int bytesRead = 0;
            while ((bytesRead = source.Read(buf, 0, bufSize)) > 0)
                target.Write(buf, 0, bytesRead);
        }

        /// <summary>
        /// Registered Property
        /// </summary>
        public bool Registered
        {
            get 
            { 
                return _registered; 
            }
            set 
            { 
                _registered = value;
                OnPropertyChanged(_colRegistered);
                OnPropertyChanged(_colUnregisterdText);
                OnPropertyChanged(_colUnregistered);
            }
        }

        /// <summary>
        /// Property Changed Event
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Fire the PropertyChnaged event handler on change of property
        /// </summary>
        /// <param name="propName"></param>
        protected void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
    }
}
