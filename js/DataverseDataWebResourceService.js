(function (DataverseDataWebResourceService, undefined) {
    "use strict";

    var BASE_SOLUTION_UNIQUE_NAME = "DataverseLabelTranslator";
    var DATA_SOLUTION_DISPLAY_NAME = "Dataverse Label Translator Data";
    var DATA_SOLUTION_UNIQUE_NAME = "DataverseLabelTranslatorData";
    var COMPONENT_TYPE_WEBRESOURCE = 61;
    var DEFAULT_WEBRESOURCE_TYPE = 4;

    function getOrgUrl() {
        try {
            if (window.Xrm && Xrm.Page && Xrm.Page.context && Xrm.Page.context.getClientUrl) {
                return Xrm.Page.context.getClientUrl().toLowerCase();
            }
        } catch (e) {
            return "";
        }

        return "";
    }

    function escapeODataString(value) {
        return String(value || "").replace(/'/g, "''");
    }

    function b64EncodeUnicode(str) {
        return btoa(unescape(encodeURIComponent(str || "")));
    }

    function b64DecodeUnicode(str) {
        return decodeURIComponent(escape(atob(str || "")));
    }

    function parseGuidFromCreateResponse(createResponse) {
        if (!createResponse) {
            return null;
        }

        if (typeof createResponse === "string") {
            var stringMatch = createResponse.match(/[0-9a-fA-F-]{36}/);
            return stringMatch ? stringMatch[0] : null;
        }

        if (createResponse.id) {
            return createResponse.id;
        }

        if (createResponse.headers) {
            var entityId = createResponse.headers["OData-EntityId"] || createResponse.headers["OData-EntityID"] || createResponse.headers["odata-entityid"];
            var headerMatch = entityId && entityId.match(/[0-9a-fA-F-]{36}/);
            return headerMatch ? headerMatch[0] : null;
        }

        return null;
    }

    function normalizePublisherPrefix(prefix) {
        var sanitized = String(prefix || "new")
            .replace(/[^A-Za-z0-9]/g, "")
            .toLowerCase();

        if (!sanitized) {
            return "new";
        }

        return sanitized.substring(0, 8);
    }

    function buildPublisherPrefixCandidates(basePrefix) {
        var primary = normalizePublisherPrefix(basePrefix);
        var secondary = normalizePublisherPrefix(primary + "d");

        if (primary === secondary) {
            return [primary];
        }

        return [primary, secondary];
    }

    function findBaseSolutionByName(solutionUniqueName) {
        return WebApiClient.Retrieve({
            entityName: "solution",
            queryParams: "?$select=solutionid,uniquename,friendlyname,_publisherid_value&$filter=uniquename eq '" + escapeODataString(solutionUniqueName) + "'"
        })
        .then(function (response) {
            return response && response.value && response.value.length > 0 ? response.value[0] : null;
        });
    }

    function findBaseSolution() {
        return findBaseSolutionByName(BASE_SOLUTION_UNIQUE_NAME)
        .then(function (solution) {
            if (!solution) {
                throw new Error("Base solution " + BASE_SOLUTION_UNIQUE_NAME + " not found.");
            }

            return solution;
        });
    }

    function findPublisherByUniqueName(uniqueName) {
        return WebApiClient.Retrieve({
            entityName: "publisher",
            queryParams: "?$select=publisherid,uniquename,friendlyname,customizationprefix&$filter=uniquename eq '" + escapeODataString(uniqueName) + "'"
        })
        .then(function (response) {
            return response && response.value && response.value.length > 0 ? response.value[0] : null;
        });
    }

    function getPublisherById(publisherId) {
        return WebApiClient.Retrieve({
            entityName: "publisher",
            entityId: publisherId,
            queryParams: "?$select=publisherid,uniquename,friendlyname,customizationprefix"
        });
    }

    function tryCreateDataPublisher(uniqueName, friendlyName, prefixCandidates, index) {
        if (index >= prefixCandidates.length) {
            throw new Error("Failed to create data publisher for app data storage.");
        }

        return WebApiClient.Create({
            entityName: "publisher",
            entity: {
                uniquename: uniqueName,
                friendlyname: friendlyName,
                customizationprefix: prefixCandidates[index]
            }
        })
        .then(function (createResponse) {
            var publisherId = parseGuidFromCreateResponse(createResponse);
            if (!publisherId) {
                return findPublisherByUniqueName(uniqueName);
            }

            return getPublisherById(publisherId);
        })
        .catch(function () {
            return tryCreateDataPublisher(uniqueName, friendlyName, prefixCandidates, index + 1);
        });
    }

    function ensureDataPublisher(basePublisher) {
        var dataPublisherUniqueName = (basePublisher.uniquename || BASE_SOLUTION_UNIQUE_NAME) + "Data";
        var dataPublisherFriendlyName = (basePublisher.friendlyname || BASE_SOLUTION_UNIQUE_NAME) + " Data";

        return findPublisherByUniqueName(dataPublisherUniqueName)
        .then(function (existingPublisher) {
            if (existingPublisher) {
                return existingPublisher;
            }

            var prefixCandidates = buildPublisherPrefixCandidates(basePublisher.customizationprefix);
            return tryCreateDataPublisher(dataPublisherUniqueName, dataPublisherFriendlyName, prefixCandidates, 0);
        });
    }

    function findDataSolution() {
        return WebApiClient.Retrieve({
            entityName: "solution",
            queryParams: "?$select=solutionid,uniquename,friendlyname,_publisherid_value,version&$filter=uniquename eq '" + DATA_SOLUTION_UNIQUE_NAME + "'"
        })
        .then(function (response) {
            return response && response.value && response.value.length > 0 ? response.value[0] : null;
        });
    }

    function createDataSolution(publisherId) {
        return WebApiClient.Create({
            entityName: "solution",
            entity: {
                friendlyname: DATA_SOLUTION_DISPLAY_NAME,
                uniquename: DATA_SOLUTION_UNIQUE_NAME,
                version: "1.0.0.0",
                "publisherid@odata.bind": "/publishers(" + publisherId + ")"
            }
        })
        .then(function () {
            return findDataSolution();
        });
    }

    function ensureDataSolutionRecord(publisherId) {
        return findDataSolution()
        .then(function (solution) {
            if (solution) {
                return solution;
            }

            return createDataSolution(publisherId);
        });
    }

    function runEnsureDataSolution(forceRefresh) {
        var result = {};

        return findBaseSolution()
        .then(function (baseSolution) {
            result.baseSolution = baseSolution;
            return getPublisherById(baseSolution._publisherid_value);
        })
        .then(function (basePublisher) {
            result.basePublisher = basePublisher;
            return ensureDataPublisher(basePublisher);
        })
        .then(function (dataPublisher) {
            result.dataPublisher = dataPublisher;
            return ensureDataSolutionRecord(dataPublisher.publisherid);
        })
        .then(function (dataSolution) {
            return {
                orgUrl: getOrgUrl(),
                baseSolutionUniqueName: BASE_SOLUTION_UNIQUE_NAME,
                solutionUniqueName: DATA_SOLUTION_UNIQUE_NAME,
                solutionId: dataSolution.solutionid,
                publisherId: result.dataPublisher.publisherid,
                initializedOn: new Date().toISOString(),
                schemaVersion: 1
            };
        });
    }

    function ensureDataSolution(forceRefresh) {
        return runEnsureDataSolution(!!forceRefresh);
    }

    function getWebResourceById(webResourceId) {
        return WebApiClient.Retrieve({
            overriddenSetName: "webresourceset",
            entityId: webResourceId,
            queryParams: "?$select=webresourceid,name,displayname,content,modifiedon,webresourcetype"
        });
    }

    function findWebResourceByName(webResourceName) {
        return WebApiClient.Retrieve({
            overriddenSetName: "webresourceset",
            queryParams: "?$select=webresourceid,name,displayname,content,modifiedon,webresourcetype&$filter=name eq '" + escapeODataString(webResourceName) + "'"
        })
        .then(function (response) {
            return response && response.value && response.value.length > 0 ? response.value[0] : null;
        });
    }

    function createTextWebResource(options) {
        return WebApiClient.Create({
            overriddenSetName: "webresourceset",
            entity: {
                name: options.uniqueName,
                displayname: options.displayName || options.uniqueName,
                description: options.description || "",
                webresourcetype: options.webResourceType || DEFAULT_WEBRESOURCE_TYPE,
                content: b64EncodeUnicode(options.defaultContent || "")
            }
        })
        .then(function (createResponse) {
            var webResourceId = parseGuidFromCreateResponse(createResponse);
            if (!webResourceId) {
                return findWebResourceByName(options.uniqueName);
            }

            return getWebResourceById(webResourceId);
        });
    }

    function isWebResourceInSolution(solutionId, webResourceId) {
        return WebApiClient.Retrieve({
            apiVersion: "9.2",
            entityName: "solutioncomponent",
            queryParams: "?$select=solutioncomponentid&$filter=_solutionid_value eq " + solutionId + " and componenttype eq " + COMPONENT_TYPE_WEBRESOURCE + " and objectid eq " + webResourceId
        })
        .then(function (response) {
            return !!(response && response.value && response.value.length > 0);
        });
    }

    function isDuplicateSolutionComponentError(error) {
        var message = String(error && (error.message || error.statusText || error) || "");
        return /duplicate|CrmDuplicateRecordException|Cannot insert duplicate key/i.test(message);
    }

    function addWebResourceToSolution(solutionUniqueName, webResourceId) {
        return WebApiClient.SendRequest("POST", WebApiClient.GetApiUrl({ apiVersion: "9.2" }) + "AddSolutionComponent()", {
            ComponentId: webResourceId,
            ComponentType: COMPONENT_TYPE_WEBRESOURCE,
            SolutionUniqueName: solutionUniqueName,
            AddRequiredComponents: false,
            IncludedComponentSettingsValues: null,
            DoNotIncludeSubcomponents: false
        })
        .catch(function (error) {
            if (isDuplicateSolutionComponentError(error)) {
                return null;
            }

            throw error;
        });
    }

    function ensureWebResourceInSolution(solutionInfo, webResourceId) {
        return isWebResourceInSolution(solutionInfo.solutionId, webResourceId)
        .then(function (isAdded) {
            if (isAdded) {
                return null;
            }

            return addWebResourceToSolution(solutionInfo.solutionUniqueName, webResourceId);
        });
    }

    function normalizeOptions(options) {
        options = options || {};

        if (!options.uniqueName) {
            throw new Error("A web resource uniqueName is required.");
        }

        return {
            uniqueName: options.uniqueName,
            displayName: options.displayName || options.uniqueName,
            description: options.description || "",
            webResourceType: options.webResourceType || DEFAULT_WEBRESOURCE_TYPE,
            defaultContent: options.defaultContent || ""
        };
    }

    function ensureTextWebResource(options) {
        var normalizedOptions = normalizeOptions(options);
        var result = {};

        return ensureDataSolution(false)
        .then(function (solutionInfo) {
            result.solutionInfo = solutionInfo;
            return findWebResourceByName(normalizedOptions.uniqueName);
        })
        .then(function (webResource) {
            if (webResource) {
                return webResource;
            }

            return createTextWebResource(normalizedOptions);
        })
        .then(function (webResource) {
            result.webResource = webResource;
            return ensureWebResourceInSolution(result.solutionInfo, webResource.webresourceid);
        })
        .then(function () {
            return {
                orgUrl: getOrgUrl(),
                solutionUniqueName: result.solutionInfo.solutionUniqueName,
                solutionId: result.solutionInfo.solutionId,
                publisherId: result.solutionInfo.publisherId,
                webResourceId: result.webResource.webresourceid,
                webResourceName: result.webResource.name,
                webResource: result.webResource,
                initializedOn: new Date().toISOString(),
                schemaVersion: 1
            };
        });
    }

    function decodeWebResourceContent(rawContent, fallbackContent) {
        if (!rawContent) {
            return fallbackContent || "";
        }

        try {
            return b64DecodeUnicode(rawContent);
        } catch (e) {
            return String(rawContent || "");
        }
    }

    function publishWebResource(webResourceId) {
        if (!webResourceId) {
            return Promise.resolve(null);
        }

        if (window.XrmTranslator && typeof XrmTranslator.PublishWebResources === "function") {
            return XrmTranslator.PublishWebResources([webResourceId]);
        }

        var xmlPayload = "<importexportxml><webresources><webresource>" + webResourceId + "</webresource></webresources></importexportxml>";
        var request = WebApiClient.Requests.PublishXmlRequest.with({
            payload: {
                ParameterXml: xmlPayload
            }
        });

        return WebApiClient.Execute(request);
    }

    DataverseDataWebResourceService.EnsureDataSolution = function (forceRefresh) {
        return ensureDataSolution(!!forceRefresh);
    };

    DataverseDataWebResourceService.EnsureTextWebResource = function (options) {
        return ensureTextWebResource(options);
    };

    DataverseDataWebResourceService.ReadText = function (options) {
        var normalizedOptions = normalizeOptions(options);

        return ensureTextWebResource(normalizedOptions)
        .then(function (info) {
            return findWebResourceByName(normalizedOptions.uniqueName)
            .then(function (webResource) {
                if (!webResource) {
                    return normalizedOptions.defaultContent || "";
                }

                return decodeWebResourceContent(webResource.content, normalizedOptions.defaultContent);
            });
        });
    };

    DataverseDataWebResourceService.WriteText = function (options, content) {
        var normalizedOptions = normalizeOptions(options);
        var targetId = null;

        return ensureTextWebResource(normalizedOptions)
        .then(function (info) {
            targetId = info.webResourceId;

            return WebApiClient.Update({
                overriddenSetName: "webresourceset",
                entityId: targetId,
                entity: {
                    content: b64EncodeUnicode(content || "")
                }
            });
        })
        .then(function () {
            return publishWebResource(targetId);
        })
        .then(function () {
            return getWebResourceById(targetId);
        });
    };

    DataverseDataWebResourceService.ReadJson = function (options) {
        return DataverseDataWebResourceService.ReadText(options)
        .then(function (content) {
            if (!content) {
                return {};
            }

            return JSON.parse(content);
        });
    };

    DataverseDataWebResourceService.WriteJson = function (options, value) {
        return DataverseDataWebResourceService.WriteText(options, JSON.stringify(value || {}, null, 2));
    };

    DataverseDataWebResourceService.PublishWebResource = function (webResourceId) {
        return publishWebResource(webResourceId);
    };

}(window.DataverseDataWebResourceService = window.DataverseDataWebResourceService || {}));
