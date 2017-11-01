using Autodesk.Forge;
using Autodesk.Forge.Model;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace DataManagementSample.Controllers
{
  public class RevitIOController : ApiController
  {
    public class Input
    {
      public string versionUrl { get; set; }
      public string input { get; set; }
    }

    private string AccessToken3Legged
    {
      get
      {
        var cookies = Request.Headers.GetCookies();
        var accessToken = cookies[0].Cookies[0].Value;
        return accessToken;
      }
    }

    private const string PACKAGE_NAME = "ChangeParameterAppPackage";
    private const string ACTIVITY_NAME = "ChangeParameterActivity";

    [HttpPost]
    [Route("api/forge/revitio/updateParameters")]
    public async Task<string> UpdateParameters([FromBody]Input input)
    {
      string packageZipPath = Path.Combine(HttpContext.Current.Server.MapPath("~/DesignAutomation/RevitIO-ChangeParameter/RevitIO-ChangeParameter/bin/Release"), "ChangeParameter.zip");

      // ********************************************
      // need a 2-legged CODE:ALL token
      TwoLeggedApi apiInstance = new TwoLeggedApi();
      dynamic bearer = await apiInstance.AuthenticateAsync(ConfigVariables.FORGE_CLIENT_ID, ConfigVariables.FORGE_CLIENT_SECRET, oAuthConstants.CLIENT_CREDENTIALS, new Scope[] { Scope.CodeAll });
      string accessToken2legged = bearer.access_token;

      // ********************************************
      // Create AppPackage
      AppPackagesApi appPackageApi = new AppPackagesApi();
      appPackageApi.Configuration.AccessToken = accessToken2legged;
      try
      {
        dynamic package = await appPackageApi.GetAppPackageAsync(PACKAGE_NAME);
        await appPackageApi.DeleteAppPackageAsync(PACKAGE_NAME); // for testing
      }
      catch
      {
        // in this case the package DOES NOT exists
      }


      try
      {
        IRestClient restClient = new RestClient("https://developer.api.autodesk.com");

        // this need to be done manually as the API is not working 
        // string url = await appPackageApi.GetUploadUrlAsync();
        IRestRequest reqGetUploadUrl = new RestRequest("/revit.io/us-east/v2/AppPackages/Operations.GetUploadUrl", Method.GET);
        reqGetUploadUrl.AddHeader("Authorization", "Bearer " + accessToken2legged);
        IRestResponse resGetUploadUrl = await restClient.ExecuteTaskAsync(reqGetUploadUrl);
        if (resGetUploadUrl.StatusCode != HttpStatusCode.OK) return "Cannot create package";
        JObject getUploadUrl = JObject.Parse(resGetUploadUrl.Content);
        Uri packageUrl = new Uri(getUploadUrl.Property("value").Value.ToString());

        //restClient = new RestClient(packageUrl);
        //IRestRequest reqPutAppPackage = new RestRequest();
        //reqPutAppPackage.AddHeader("Authorization", "Bearer " + accessToken2legged);
        //reqPutAppPackage.AddFile("ChangeParameter.zip", packageZipPath);
        //reqPutAppPackage.AddParameter("ChangeParameter.zip", File.ReadAllBytes(packageZipPath), "application/zip", ParameterType.RequestBody);
        //IRestResponse resPutAppPackage = await restClient.ExecuteTaskAsync(reqPutAppPackage);
        //if (resPutAppPackage.StatusCode != HttpStatusCode.OK) return "Cannot create package";
        var client = new HttpClient();
        client.PutAsync(packageUrl.AbsoluteUri, new StreamContent(File.OpenRead(packageZipPath))).Result.EnsureSuccessStatusCode();

        dynamic appPackage = await appPackageApi.CreateAppPackageAsync(new AppPackage(PACKAGE_NAME, packageUrl.AbsoluteUri, new List<string>(), "29.4", 1, "Change parameters on models", false, false));
      }
      catch (Exception e)
      {
      }

      // ********************************************
      // Create Activity
      ActivitiesApi activityApi = new ActivitiesApi();
      activityApi.Configuration.AccessToken = accessToken2legged;
      try
      {
        dynamic existingActivity = await activityApi.GetActivityAsync(ACTIVITY_NAME);
        await activityApi.DeleteActivityAsync(ACTIVITY_NAME);
      }
      catch
      {

      }

      try
      { 
        JObject instructions = new JObject
        {
          new JProperty("CommandLineParameters",  ""),
          new JProperty("Script",  "")
        };
        JObject parameters = new JObject
        {
          new JProperty("InputParameters", new JArray{
            new JObject
            {
              new JProperty("Name", "HostDwg"),
              new JProperty("LocalFileName", "$(HostDwg)"),
            },
            new JObject
            {
              new JProperty("Name", "Params"),
              new JProperty("LocalFileName", "params.json"),
            }
          }),
          new JProperty("OutputParameters", new JArray
          {
            new JObject{
              new JProperty("Name", "Result"),
              new JProperty("LocalFileName", "result.rvt")
            }
          })
        };
        Activity activity = new Activity(ACTIVITY_NAME, instructions, new List<string>() { PACKAGE_NAME }, "29.4", parameters, null, 1, null, null, false);
        dynamic newActivity = await activityApi.CreateActivityAsync(activity);
      }
      catch(Exception e)
      {
      }


      // ********************************************
      // Prepare the intput & output location
      // In this case we need a new version for this item
      // and the respective storage location
      string[] idParams = input.versionUrl.Split('/');
      string projectId = idParams[idParams.Length - 3];
      string versionId = idParams[idParams.Length - 1];

      string downloadUrl = string.Empty;
      string uploadUrl = string.Empty;
      try
      {
        VersionsApi versionApi = new VersionsApi();
        versionApi.Configuration.AccessToken = AccessToken3Legged;
        dynamic version = await versionApi.GetVersionAsync(projectId, versionId);
        dynamic versionItem = await versionApi.GetVersionItemAsync(projectId, versionId);

        string[] versionItemParams = ((string)version.data.relationships.storage.data.id).Split('/');
        string[] bucketKeyParams = versionItemParams[versionItemParams.Length - 2].Split(':');
        string bucketKey = bucketKeyParams[bucketKeyParams.Length - 1];
        string objectName = versionItemParams[versionItemParams.Length - 1];
        downloadUrl = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, objectName);

        ItemsApi itemApi = new ItemsApi();
        itemApi.Configuration.AccessToken = AccessToken3Legged;
        string itemId = versionItem.data.id;
        dynamic item = await itemApi.GetItemAsync(projectId, itemId);
        string folderId = item.data.relationships.parent.data.id;
        string fileName = item.data.attributes.displayName;

        ProjectsApi projectApi = new ProjectsApi();
        projectApi.Configuration.AccessToken = AccessToken3Legged;
        StorageRelationshipsTargetData storageRelData = new StorageRelationshipsTargetData(StorageRelationshipsTargetData.TypeEnum.Folders, folderId);
        CreateStorageDataRelationshipsTarget storageTarget = new CreateStorageDataRelationshipsTarget(storageRelData);
        CreateStorageDataRelationships storageRel = new CreateStorageDataRelationships(storageTarget);
        BaseAttributesExtensionObject attributes = new BaseAttributesExtensionObject(string.Empty, string.Empty, new JsonApiLink(string.Empty), null);
        CreateStorageDataAttributes storageAtt = new CreateStorageDataAttributes(fileName, attributes);
        CreateStorageData storageData = new CreateStorageData(CreateStorageData.TypeEnum.Objects, storageAtt, storageRel);
        CreateStorage storage = new CreateStorage(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), storageData);

        dynamic storageCreated = await projectApi.PostStorageAsync(projectId, storage);

        string[] storageIdParams = ((string)storageCreated.data.id).Split('/');
        bucketKeyParams = storageIdParams[storageIdParams.Length - 2].Split(':');
        bucketKey = bucketKeyParams[bucketKeyParams.Length - 1];
        objectName = storageIdParams[storageIdParams.Length - 1];

        uploadUrl = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, objectName);

        JObject arguments = new JObject
        {
          new JProperty(
            "InputArguments", new JArray
            {
              new JObject
              {
                new JProperty("Resource", downloadUrl),
                new JProperty("Headers", new JArray{
                  new JObject
                  {
                    new JProperty("Name", "Authorization"),
                    new JProperty("Value", "Bearer " + AccessToken3Legged)
                  }
                }),
                new JProperty("Name",  "HostDwg"),
              },
              {
                new JObject
                {
                  new JProperty("ResourceKind", "Embedded"),
                  new JProperty("Resource", "data:application/json, " + input.input),
                  new JProperty("Name",  "Params")
                }
              }
            }
          ),
          new JProperty(
            "OutputArguments", new JArray
            {
              new JObject
              {
                new JProperty("Name", "Result"),
                new JProperty("HttpVerb", "PUT"),
                new JProperty("Resource", uploadUrl),
                new JProperty("Headers", new JArray{
                  new JObject
                  {
                    new JProperty("Name", "Authorization"),
                    new JProperty("Value", "Bearer " + AccessToken3Legged)
                  }
                })//,
                //new JProperty("StorageProvider",  "Generic")
              }
            }
          )
        };
        WorkItemsApi workItemsApi = new WorkItemsApi();
        workItemsApi.Configuration.AccessToken = accessToken2legged;

        dynamic workitem = await workItemsApi.CreateWorkItemAsync(new WorkItem(string.Empty, arguments, null, null, null, ACTIVITY_NAME));
        // wait...
        System.Threading.Thread.Sleep(5000);

        string id = workitem.Id;
        dynamic status = await workItemsApi.GetWorkItemAsync(id);
        string statusName = status.Status;
        string statusReport = status.StatusDetails.Report;

        // Create a new version 
        ProjectsApi projectsApi = new ProjectsApi();
        CreateVersion newVersionData = new CreateVersion
        (
           new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0),
           new CreateVersionData
           (
             CreateVersionData.TypeEnum.Versions,
             new CreateStorageDataAttributes
             (
               fileName,
               new BaseAttributesExtensionObject
               (
                 "versions:autodesk.core:File",
                 "1.0",
                 new JsonApiLink(null),
                 null
               )
             ),
             new CreateVersionDataRelationships
             (
                new CreateVersionDataRelationshipsItem
                (
                  new CreateVersionDataRelationshipsItemData
                  (
                    CreateVersionDataRelationshipsItemData.TypeEnum.Items,
                    item.data.id
                  )
                ),
                new CreateItemRelationshipsStorage
                (
                  new CreateItemRelationshipsStorageData
                  (
                    CreateItemRelationshipsStorageData.TypeEnum.Objects,
                    storageCreated.data.id
                  )
                )
             )
           )
        );
        dynamic newVersion = await projectsApi.PostVersionAsync(projectId, newVersionData);
        /*CreateVersion newVersionData = new CreateVersion(
          new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0),
           new CreateVersionData(
              
              new CreateStorageDataAttributes(fileName, new BaseAttributesExtensionObject("versions:autodesk.core:File", "1.0", null, null)),
              new CreateVersionDataRelationships(
                new CreateVersionDataRelationshipsItem(
                  new CreateVersionDataRelationshipsItemData(CreateVersionDataRelationshipsItemData.TypeEnum.Items,
                  item.data.id)),
                new CreateItemRelationshipsStorage(
                  new CreateItemRelationshipsStorageData(CreateItemRelationshipsStorageData.TypeEnum.Objects,
                  storageCreated.data.id)))));*/


        return statusName;
      }
      catch (Exception e)
      { }

      return string.Empty;
    }
  }
}
