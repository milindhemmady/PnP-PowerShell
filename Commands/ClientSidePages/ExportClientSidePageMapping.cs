﻿#if !ONPREMISES
using SharePointPnP.PowerShell.CmdletHelpAttributes;
using System;
using System.Management.Automation;
using SharePointPnP.PowerShell.Commands.Utilities;
using System.Reflection;
using Microsoft.SharePoint.Client;
using System.IO;
using SharePointPnP.Modernization.Framework.Publishing;

namespace SharePointPnP.PowerShell.Commands.ClientSidePages
{
    [Cmdlet(VerbsData.Export, "PnPClientSidePageMapping")]
    [CmdletHelp("Get's the built-in maping files or a custom mapping file for your publishing portal page layouts. These mapping files are used to tailor the page transformation experience.",
                Category = CmdletHelpCategory.ClientSidePages, SupportedPlatform = CmdletSupportedPlatform.Online)]
    [CmdletExample(Code = @"PS:> Export-PnPClientSidePageMapping -BuiltInPageLayoutMapping -CustomPageLayoutMapping -Folder c:\\temp -Overwrite",
                   Remarks = "Exports the built in page layout mapping and analyzes the current site's page layouts and exports these to files in folder c:\\temp",
                   SortOrder = 1)]
    [CmdletExample(Code = @"PS:> Export-PnPClientSidePageMapping -BuiltInWebPartMapping -Folder c:\\temp -Overwrite",
                   Remarks = "Exports the built in webpart mapping to a file in folder c:\\temp. Use this a starting basis if you want to tailer the web part mapping behavior.",
                   SortOrder = 2)]
    public class ExportClientSidePageMapping : PnPWebCmdlet
    {
        private Assembly modernizationAssembly;
        private Assembly sitesCoreAssembly;
        private Assembly newtonsoftAssembly;

        [Parameter(Mandatory = false, HelpMessage = "Exports the builtin web part mapping file")]
        public SwitchParameter BuiltInWebPartMapping = false;

        [Parameter(Mandatory = false, HelpMessage = "Exports the builtin pagelayout mapping file (only needed for publishing page transformation)")]
        public SwitchParameter BuiltInPageLayoutMapping = false;

        [Parameter(Mandatory = false, HelpMessage = "Analyzes the pagelayouts in the current publishing portal and exports them as a pagelayout mapping file")]
        public SwitchParameter CustomPageLayoutMapping = false;

        [Parameter(Mandatory = false, ValueFromPipeline = true, Position = 0, HelpMessage = "The folder to created the mapping file(s) in")]
        public string Folder;

        [Parameter(Mandatory = false, HelpMessage = "Overwrites existing mapping files")]
        public SwitchParameter Overwrite = false;

        protected override void ExecuteCmdlet()
        {
            //Fix loading of modernization framework
            FixAssemblyResolving();

            // Configure folder to export
            string folderToExportTo = Environment.CurrentDirectory;
            if (!string.IsNullOrEmpty(this.Folder))
            {
                if (!Directory.Exists(this.Folder))
                {
                    throw new Exception($"Folder '{this.Folder}' does not exist");
                }

                folderToExportTo = this.Folder;
            }

            // Export built in web part mapping
            if (this.BuiltInWebPartMapping)
            {
                string fileName = Path.Combine(folderToExportTo, "webpartmapping.xml");

                if (System.IO.File.Exists(fileName) && !Overwrite)
                {
                    Console.WriteLine($"Skipping the export from the built-in webpart mapping file {fileName} as this already exists. Use the -Overwrite flag to overwrite if needed.");
                }
                else
                {
                    // Load the default one from resources into a model, no need for persisting this file
                    string webpartMappingFileContents = WebPartMappingLoader.LoadFile("SharePointPnP.PowerShell.Commands.ClientSidePages.webpartmapping.xml");
                    System.IO.File.WriteAllText(fileName, webpartMappingFileContents);
                }
            }

            // Export built in page layout mapping
            if (this.BuiltInPageLayoutMapping)
            {
                string fileName = Path.Combine(folderToExportTo, "pagelayoutmapping.xml");

                if (System.IO.File.Exists(fileName) && !Overwrite)
                {
                    Console.WriteLine($"Skipping the export from the built-in pagelayout mapping file {fileName} as this already exists. Use the -Overwrite flag to overwrite if needed.");
                }
                else
                {
                    // Load the default one from resources into a model, no need for persisting this file
                    string pageLayoutMappingFileContents = WebPartMappingLoader.LoadFile("SharePointPnP.PowerShell.Commands.ClientSidePages.pagelayoutmapping.xml");
                    System.IO.File.WriteAllText(fileName, pageLayoutMappingFileContents);
                }
            }

            // Export custom page layout mapping
            if (this.CustomPageLayoutMapping)
            {
                if (!this.ClientContext.Web.IsPublishingWeb())
                {
                    throw new Exception("The -CustomPageLayoutMapping parameter only works for publishing sites.");
                }

                Guid siteId = this.ClientContext.Site.EnsureProperty(p => p.Id);
                string fileName = $"custompagelayoutmapping-{siteId.ToString()}.xml";

                if (System.IO.File.Exists(Path.Combine(folderToExportTo, fileName)) && !Overwrite)
                {
                    Console.WriteLine($"Skipping the export from the custom pagelayout mapping file {Path.Combine(folderToExportTo, fileName)} as this already exists. Use the -Overwrite flag to overwrite if needed.");
                }
                else
                {
                    var analyzer = new PageLayoutAnalyser(this.ClientContext);
                    analyzer.AnalyseAll();

                    analyzer.GenerateMappingFile(folderToExportTo, fileName);
                }
            }
        }

        private string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        private void FixAssemblyResolving()
        {
            try
            {
                newtonsoftAssembly = Assembly.LoadFrom(Path.Combine(AssemblyDirectory, "NewtonSoft.Json.dll"));
                sitesCoreAssembly = Assembly.LoadFrom(Path.Combine(AssemblyDirectory, "OfficeDevPnP.Core.dll"));
                modernizationAssembly = Assembly.LoadFrom(Path.Combine(AssemblyDirectory, "SharePointPnP.Modernization.Framework.dll"));
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            }
            catch { }
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("OfficeDevPnP.Core"))
            {
                return sitesCoreAssembly;
            }
            if (args.Name.StartsWith("Newtonsoft.Json"))
            {
                return newtonsoftAssembly;
            }
            if (args.Name.StartsWith("SharePointPnP.Modernization.Framework"))
            {
                return modernizationAssembly;
            }
            return null;
        }

    }
}
#endif