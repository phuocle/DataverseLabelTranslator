using DataverseLabelTranslator.Server.CustomActions.Synchronous;
using DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Linq;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    [TestClass]
    public class EasyTranslatorFormMetaTests
    {
        [TestMethod]
        public void Loading_FormMeta_ReadsSystemFormTypeOptionSetValue()
        {
            var action = new EasyTranslator();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var formId = Guid.NewGuid();
            QueryExpression capturedFormQuery = null;

            serviceAdmin.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var query = (QueryExpression)call[0];
                if (query.EntityName == "organization")
                {
                    return Entities(new Entity("organization") { ["languagecode"] = 1033 });
                }

                if (query.EntityName == "systemform")
                {
                    capturedFormQuery = query;
                    return Entities(CreateForm(formId, "Account Main", 2));
                }

                return Entities();
            });

            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var retrieve = call[0] as RetrieveLocLabelsRequest;
                if (retrieve != null)
                {
                    return RetrieveLabelsResponse(new Label("Account Main", 1033));
                }

                return null;
            });

            var output = (EasyTranslatorLoadOutput)action.Loading(
                null,
                serviceAdmin,
                null,
                null,
                "{\"translatorType\":\"formMeta\",\"entityName\":\"account\",\"component\":\"DisplayText\",\"solutionId\":\"all\"}");

            Assert.AreEqual("1033", output.baseLanguage);
            Assert.AreEqual("systemform", capturedFormQuery.EntityName);
            Assert.AreEqual("flat", output.grid.mode);
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("Account Main (Main)", output.grid.rows[0].SchemaName);
            Assert.AreEqual("Account Main", output.grid.rows[0]["1033"]);
        }

        private static EntityCollection Entities(params Entity[] entities)
        {
            return new EntityCollection(entities.ToList());
        }

        private static Entity CreateForm(Guid id, string name, int type)
        {
            var entity = new Entity("systemform", id);
            entity["formid"] = id;
            entity["name"] = name;
            entity["type"] = new OptionSetValue(type);
            entity["objecttypecode"] = "account";
            entity["formactivationstate"] = new OptionSetValue(1);
            entity["iscustomizable"] = new BooleanManagedProperty(true);
            return entity;
        }

        private static RetrieveLocLabelsResponse RetrieveLabelsResponse(Label label)
        {
            var response = new RetrieveLocLabelsResponse();
            response.Results["Label"] = label;
            return response;
        }
    }
}
