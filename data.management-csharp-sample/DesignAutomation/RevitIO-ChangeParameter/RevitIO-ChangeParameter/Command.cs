/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using DesignAutomationFramework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace RevitIO_ChangeParameter
{
  [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
  [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
  public class ChangeParameterApplication : IExternalDBApplication
  {
#region OnStatup handling
    public ExternalDBApplicationResult OnStartup(Autodesk.Revit.ApplicationServices.ControlledApplication app)
    {
      DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
      return ExternalDBApplicationResult.Succeeded;
    }

    public ExternalDBApplicationResult OnShutdown(Autodesk.Revit.ApplicationServices.ControlledApplication app)
    {
      return ExternalDBApplicationResult.Succeeded;
    }

    public void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
    {
      e.Succeeded = true;
      ChangeParameter(e.DesignAutomationData);
    }

#endregion

    public static void ChangeParameter(DesignAutomationData data)
    {
      if (data == null) throw new ArgumentNullException(nameof(data));

      Application rvtApp = data.RevitApp;
      if (rvtApp == null) throw new InvalidDataException(nameof(rvtApp));

      string modelPath = data.MainModelPath;
      if (String.IsNullOrWhiteSpace(modelPath)) throw new InvalidDataException(nameof(modelPath));

      Document doc = rvtApp.OpenDocumentFile(modelPath);
      if (doc == null) throw new InvalidOperationException("Could not open document.");

      // input parameter with the properties to change
      ElementInfo[] inputParameters = JsonConvert.DeserializeObject<ElementInfo[]>(File.ReadAllText("params.json"));

      using (Transaction transaction = new Transaction(doc))
      {
        transaction.Start("Change Parameters");

        foreach (ElementInfo info in inputParameters)
        {
          Element ele = doc.GetElement(info.externalId);
          if (ele == null) continue;
          foreach (ElementInfo.Property prop in info.properties)
          {
            IList<Parameter> eleParams = ele.GetParameters(prop.name);
            if (eleParams.Count != 1) continue; // should be just 1 element
            if (eleParams[0].IsReadOnly) continue; // cannot write..
            eleParams[0].Set(prop.value);
          }
        }

        transaction.Commit();
      }

      ModelPath path = ModelPathUtils.ConvertUserVisiblePathToModelPath("result.rvt");
      doc.SaveAs(path, new SaveAsOptions());
    }
  }
}
