//* Copyright 2010-2011 Research In Motion Limited.
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
using Extensibility;
using EnvDTE;
using EnvDTE80;
using System.Globalization;
using System.Diagnostics;
using Microsoft.VisualStudio.Project;
using Microsoft.VisualStudio.VCProjectEngine;
using Microsoft.VisualStudio.CommandBars;
namespace VSNDK.AddIn
{

	/// <summary> 
    /// The object for implementing an Add-in. 
    /// </summary>
    /// 1. Disable the IntelliSense Error Reporting property
    /// 2. ...
    /// To register the addin, copy the VSNDK.AddIn.dll and VSNDK.Addin.AddIn files to 
    /// the one of the folders specified by "Add-In File path" in Add-In Security option page.
	/// <seealso class='IDTExtensibility2' />
    public class Connect : Object, IDTExtensibility2, IDTCommandTarget
	{
        private VSNDKAddIn vsAddIn = null;


		/// <summary> 
        /// Implements the constructor for the Add-in object. Place your initialization code within this method. 
        /// </summary>
		public Connect()
		{
            vsAddIn = new VSNDKAddIn();
		}

		
        /// <summary>
        /// Implements the OnConnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being loaded. 
        /// </summary>
		/// <param term='application'>Root object of the host application. </param>
		/// <param term='connectMode'>Describes how the Add-in is being loaded. </param>
		/// <param term='addInInst'>Object representing this Add-in. </param>
		/// <seealso class='IDTExtensibility2' />
		public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
		{
			_applicationObject = (DTE2)application;
			_addInInstance = (EnvDTE.AddIn)addInInst;


            vsAddIn.Connect(_applicationObject, _addInInstance);
		}


        public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
        {
        }


        public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
        {
            handled = false;
        }


		/// <summary>
        /// Implements the OnDisconnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is 
        /// being unloaded. 
        /// </summary>
		/// <param term='disconnectMode'>Describes how the Add-in is being unloaded. </param>
		/// <param term='custom'>Array of parameters that are host application specific. </param>
		/// <seealso class='IDTExtensibility2' />
		public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
		{
            vsAddIn.Disconnect();
		}


		/// <summary>
        /// Implements the OnAddInsUpdate method of the IDTExtensibility2 interface. Receives notification when the collection 
        /// of Add-ins has changed. 
        /// </summary>
		/// <param term='custom'>Array of parameters that are host application specific. </param>
		/// <seealso class='IDTExtensibility2' />		
		public void OnAddInsUpdate(ref Array custom)
		{
		}


		/// <summary>
        /// Implements the OnStartupComplete method of the IDTExtensibility2 interface. Receives notification that the host 
        /// application has completed loading. 
        /// </summary>
		/// <param term='custom'>Array of parameters that are host application specific. </param>
		/// <seealso class='IDTExtensibility2' />
		public void OnStartupComplete(ref Array custom)
		{
		}

		
        /// <summary>
        /// Implements the OnBeginShutdown method of the IDTExtensibility2 interface. Receives notification that the host 
        /// application is being unloaded. 
        /// </summary>
		/// <param term='custom'>Array of parameters that are host application specific. </param>
		/// <seealso class='IDTExtensibility2' />
		public void OnBeginShutdown(ref Array custom)
		{
		}
		
		private DTE2 _applicationObject;
		private EnvDTE.AddIn _addInInstance;
	}
}