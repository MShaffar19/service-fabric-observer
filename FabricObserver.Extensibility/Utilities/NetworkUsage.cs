﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Xml;

namespace FabricObserver.Observers.Utilities
{
    /// <summary>
    /// Static class that houses networking utilities.
    /// </summary>
    public static class NetworkUsage
    {
        public static (int LowPort, int HighPort)
            TupleGetFabricApplicationPortRangeForNodeType(string nodeType, string clusterManifestXml)
        {
            if (string.IsNullOrEmpty(nodeType) || string.IsNullOrEmpty(clusterManifestXml))
            {
                return (-1, -1);
            }

            StringReader sreader = null;
            XmlReader xreader = null;

            try
            {
                // Safe XML pattern - *Do not use LoadXml*.
                var xdoc = new XmlDocument { XmlResolver = null };
                sreader = new StringReader(clusterManifestXml);
                xreader = XmlReader.Create(sreader, new XmlReaderSettings() { XmlResolver = null });
                xdoc.Load(xreader);

                // Cluster Information.
                var nsmgr = new XmlNamespaceManager(xdoc.NameTable);
                nsmgr.AddNamespace("sf", "http://schemas.microsoft.com/2011/01/fabric");

                // SF Application Port Range.
                var applicationEndpointsNode = xdoc.SelectSingleNode($"//sf:NodeTypes//sf:NodeType[@Name='{nodeType}']//sf:ApplicationEndpoints", nsmgr);

                if (applicationEndpointsNode == null)
                {
                    return (-1, -1);
                }

                var ret = (int.Parse(applicationEndpointsNode.Attributes?.Item(0)?.Value ?? "-1"),
                           int.Parse(applicationEndpointsNode.Attributes?.Item(1)?.Value ?? "-1"));

                return ret;
            }
            catch (XmlException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NullReferenceException)
            {
            }
            finally
            {
                sreader?.Dispose();
                xreader?.Dispose();
            }

            return (-1, -1);
        }

        public static int GetActiveFirewallRulesCount()
        {
            ManagementObjectCollection results = null;
            ManagementObjectSearcher searcher = null;
            int count = -1;

            // This method is not implemented for Linux yet.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var scope = new ManagementScope("\\\\.\\ROOT\\StandardCimv2");
                    var q = new ObjectQuery("SELECT * FROM MSFT_NetFirewallRule WHERE Enabled=1");
                    searcher = new ManagementObjectSearcher(scope, q);
                    results = searcher.Get();
                    count = results.Count;
                }
                catch (ManagementException)
                {
                }
                finally
                {
                    results?.Dispose();
                    searcher?.Dispose();
                }
            }

            return count;
        }
    }
}
