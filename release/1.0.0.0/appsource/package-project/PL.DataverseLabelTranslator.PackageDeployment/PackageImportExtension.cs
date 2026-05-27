using System.ComponentModel.Composition;
using Microsoft.Xrm.Tooling.PackageDeployment.CrmPackageExtentionBase;

namespace PL.DataverseLabelTranslator.PackageDeployment
{
    [Export(typeof(IImportExtensions))]
    public class PackageImportExtension : ImportExtension
    {
        public override bool BeforeImportStage()
        {
            return true;
        }

        public override bool AfterPrimaryImport()
        {
            return true;
        }

        public override void InitializeCustomExtension()
        {
        }

        public override string GetNameOfImport(bool plural)
        {
            return plural ? "Dataverse Label Translator packages" : "Dataverse Label Translator package";
        }

        public override string GetLongNameOfImport
        {
            get { return "Dataverse Label Translator"; }
        }

        public override string GetImportPackageDataFolderName
        {
            get { return "PkgFolder"; }
        }

        public override string GetImportPackageDescriptionText
        {
            get { return "Installs the Dataverse Label Translator managed solution."; }
        }
    }
}