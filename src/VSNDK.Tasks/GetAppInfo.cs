﻿//* Copyright 2010-2011 Research In Motion Limited.
//*
//* Licensed under the Apache License, Version 2.0 (the "License");
//* you may not use this file except in compliance with the License.
//* You may obtain a copy of the License at
//*
//* http://www.apache.org/licenses/LICENSE-2.0
//*
//* Unless required by applicable law or agreed to in writing, software
//* distributed under the License is distributed on an "AS IS" BASIS,
//* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//* See the License for the specific language governing permissions and
//* limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace VSNDK.Tasks
{
    /// <summary>
    /// MSBuid Task to retrieve the application info
    /// </summary>
    public class GetAppInfo : Task
    {
        #region Member Variables and Constants
        private string _projectDir;
        private string _appName;
        private string _barDescriptorPath = "";
        private string _appId;
        #endregion

        /// <summary>
        /// Execute the MSBuild Task
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            string rootedOutDir = (Path.IsPathRooted(_barDescriptorPath)) ? _barDescriptorPath : _projectDir + _barDescriptorPath;
            XmlReader reader = XmlReader.Create(rootedOutDir);
            reader.ReadToFollowing("id");
            _appId = reader.ReadElementContentAsString();

            return true;
        }

        /// <summary>
        /// Getter/Setter for the ApplicationDescriptorXml property
        /// </summary>
        public string ApplicationDescriptorXml
        {
            set 
            {
                _barDescriptorPath = value;
            }
            get 
            {
                return _barDescriptorPath; 
            }
        }

        /// <summary>
        /// Getter/Setter for the ProjectDir property
        /// </summary>
        public string ProjectDir
        {
            set 
            {
                _projectDir = value;
            }
            get 
            { 
                return _projectDir; 
            }
        }

        /// <summary>
        /// Getter/Setter for the AppName property
        /// </summary>
        public string AppName
        {
            set
            {
                _appName = value;

                if (_barDescriptorPath == "")
                {
                    // Default location of bar-descriptor file, if not specified in the Properties Configurations.
                    _barDescriptorPath = "BlackBerry-" + _appName + "\\bar-descriptor.xml";
                    if (!File.Exists(_barDescriptorPath))
                    {
                        // Just to support the default locations of bar-descriptor from previous versions of the plug-in.
                        _barDescriptorPath = _appName + "_barDescriptor\\bar-descriptor.xml";
                        if (!File.Exists(_barDescriptorPath))
                        {
                            _barDescriptorPath = "bar-descriptor.xml";
                        }
                    }
                }
            }
            get
            {
                return _appName;
            }
        }

        /// <summary>
        /// Getter/Setter for the AppId property
        /// </summary>
        [Output]
        public string AppId
        {
            get
            {
                return _appId;
            }
        }
    }
}
