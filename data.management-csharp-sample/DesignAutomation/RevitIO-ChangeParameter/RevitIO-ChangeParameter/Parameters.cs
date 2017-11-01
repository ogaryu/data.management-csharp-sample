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

using System.Collections.Generic;

namespace RevitIO_ChangeParameter
{
  /*
   * The input JSON will be in the form of
   [
       {  
        "externalId":"cf768514-597e-453c-b954-d6c707b9c44f-0004f29e",
          "properties":[
             {  
              "name":"property name: e.g. Comments",
                "value":"new value"
           },
           {  
              "name":property name: e.g. "Mark",
              "value":"new value"
           }
        ]
     },
     {  
        "externalId":"cf768514-597e-453c-b954-d6c707b9c44f-0004f29f",
        "properties":[
           {  
              "name":"property name: e.g. Comments",
              "value":"new value"
           },
           {  
              "name":"property name: e.g. Mark",
              "value":"new value"
           }
        ]
     }
  ]
    */
  public class ElementInfo
  {
    public string externalId { get; set; }
    public List<Property> properties { get; set; }
    public class Property
    {
      public string name { get; set; }
      public string value { get; set; }
    }

  }
}
