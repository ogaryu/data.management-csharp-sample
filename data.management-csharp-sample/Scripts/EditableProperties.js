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

AutodeskNamespace("Autodesk.ADN.Viewing.Extension");


Autodesk.ADN.Viewing.Extension.MetaProperties = function (viewer, options) {

  // base constructor
  Autodesk.Viewing.Extension.call(this, viewer, options);

  var _self = this;

  ///////////////////////////////////////////////////////////////////////////
  // load callback
  //
  ///////////////////////////////////////////////////////////////////////////
  _self.load = function () {

    var panel = new Autodesk.ADN.Viewing.Extension.MetaPropertyPanel(
      viewer, options);

    viewer.setPropertyPanel(panel);

    console.log("Autodesk.ADN.Viewing.Extension.MetaProperties loaded");

    return true;
  };

  ///////////////////////////////////////////////////////////////////////////
  // unload callback
  //
  ///////////////////////////////////////////////////////////////////////////
  _self.unload = function () {

    viewer.setPropertyPanel(null);

    console.log("Autodesk.ADN.Viewing.Extension.MetaProperties unloaded");

    return true;
  };

  ///////////////////////////////////////////////////////////////////////////
  // MetaPropertyPanel
  // Overrides native viewer property panel
  ///////////////////////////////////////////////////////////////////////////
  Autodesk.ADN.Viewing.Extension.MetaPropertyPanel = function (viewer, options) {

    var _panel = this;
    var _properties = options.properties;

    document.addEventListener("blur", onLeaveTextbox, true);

    Autodesk.Viewing.Extensions.ViewerPropertyPanel.call(
      _panel,
      viewer);

    /////////////////////////////////////////////////////////////////
    // setNodeProperties override
    //
    /////////////////////////////////////////////////////////////////
    _panel.setNodeProperties = function (nodeId) {

      Autodesk.Viewing.Extensions.ViewerPropertyPanel.
        prototype.setNodeProperties.call(
        _panel,
        nodeId);

      _panel.nodeId = nodeId;
    };

    /////////////////////////////////////////////////////////////////
    // Adds new meta property to panel
    //
    /////////////////////////////////////////////////////////////////
    _panel.addMetaProperty = function (metaProperty, options) {

      var element = this.tree.getElementForNode({
        name: metaProperty.name,
        value: metaProperty.value,
        category: metaProperty.category
      });

      if (element) {
        return false;
      }

      var parent = null;

      if (metaProperty.category) {

        parent = this.tree.getElementForNode({ name: metaProperty.category });

        if (!parent) {
          parent = this.tree.createElement_({
            name: metaProperty.category,
            type: 'category'
          },
            this.tree.myRootContainer, options && options.localizeCategory ? { localize: true } : null);
        }

      } else {

        parent = this.tree.myRootContainer;
      }

      this.tree.createElement_(
        metaProperty,
        parent,
        options && options.localizeProperty ? { localize: true } : null);

      return true;
    };

    /////////////////////////////////////////////////////////////////
    // setProperties override
    //
    /////////////////////////////////////////////////////////////////
    _panel.setProperties = function (properties) {

      Autodesk.Viewing.Extensions.ViewerPropertyPanel.
        prototype.setProperties.call(
        _panel,
        properties);
    };

    /////////////////////////////////////////////////////////////////
    // displayProperty override
    //
    /////////////////////////////////////////////////////////////////
    _panel.displayProperty = function (property, parent, options) {

      var name = document.createElement('div');

      var text = property.name;

      if (options && options.localize) {
        name.setAttribute('data-i18n', text);
        text = Autodesk.Viewing.i18n.translate(text);
      }

      name.textContent = text;
      name.title = text;
      name.className = 'propertyName';

      var separator = document.createElement('div');
      separator.className = 'separator';

      parent.appendChild(name);
      parent.appendChild(separator);

      var value = null;

      //native properties dont have a dataType
      //display them just as text
      if (!property.dataType) {
        if (_properties.indexOf(property.name) > -1) {//(property.name === 'Comments') {
          value = createTextboxProperty(property, parent);
        }
        else {
          value = createTextProperty(property, parent);
        }
        return [name, value];
        return;
      }

      switch (property.dataType) {

        case 'text':
          value = createTextProperty(property, parent);
          break;

        case 'link':
          value = createLinkProperty(property, parent);
          break;

        case 'img':
          value = createImageProperty(property, parent);
          break;

        case 'file':
          value = createFileProperty(property, parent);
          break;

        default:
          break;
      }

      // Make the property name and value highlightable.
      return [name, value];
    }

    /////////////////////////////////////////////////////////////////
    // Creates a text property
    //
    /////////////////////////////////////////////////////////////////
    function createTextProperty(property, parent) {

      var value = document.createElement('div');
      value.textContent = property.value;
      value.title = property.value;
      value.className = 'propertyValue';

      parent.appendChild(value);

      return value;
    }

    function createTextboxProperty(property, parent) {

      var value = document.createElement('div');
      var input = document.createElement('input');
      input.type = 'text';
      input.setAttribute('value', getValue(_panel.nodeId, property));
      input.id = 'editbox_' + property.name + '_' + _panel.nodeId;
      input.className = 'propertyTextbox';

      value.appendChild(input);
      value.title = property.value;

      parent.appendChild(value);

      return value;
    }

    function getValue(nodeId, props) {
      if (valuesToSubmit[nodeId] == null)
        return props.value;
      if (valuesToSubmit[nodeId].properties[props.name] == null)
        return props.value
      return valuesToSubmit[nodeId].properties[props.name];
    }

    var valuesToSubmit = {};

    function onLeaveTextbox(e) {
      // check that the event target has the desired class
      if (e.target.classList.contains("propertyTextbox")) {
        var params = e.target.id.split('_');
        var property = params[1];
        var id = params[2];
        var value = e.target.value;

        if (valuesToSubmit[id] == null) {
          viewer.getProperties(id, function (e) {
            valuesToSubmit[id] = { 'externalId': 0, 'properties': {} };
            valuesToSubmit[id].externalId = e.externalId;
            valuesToSubmit[id].properties = {};
            valuesToSubmit[id].properties[property] = value;
            showSaveButton();
          })
        }
        else {
          valuesToSubmit[id].properties[property] = value;
          showSaveButton();
        }
      }
    }

    var button1 = null;

    function showSaveButton() {
      if (button1 == null && Object.keys(valuesToSubmit).length > 0) {
        button1 = new Autodesk.Viewing.UI.Button('saveToRevit');
        button1.onClick = function (e) {
          confirm('Save to a new version of this file?');
        }
        button1.addClass('saveToRevitButton');
        button1.setToolTip('Save changes');

        // SubToolbar
        this.subToolbar = new Autodesk.Viewing.UI.ControlGroup('RevitIOSample');
        this.subToolbar.addControl(button1);

        viewer.toolbar.addControl(this.subToolbar);
      }
    }

    /////////////////////////////////////////////////////////////////
    // Creates a link property
    //
    /////////////////////////////////////////////////////////////////
    function createLinkProperty(property, parent) {

      var id = guid();

      var html = [

        '<div id="' + id + '" class="propertyValue">',
        '<a  href="' + property.href + '" target="_blank"> ' + property.value + '</a>',
        '</div>'

      ].join('\n');

      $(parent).append(html);

      return $('#' + id)[0];
    }

    /////////////////////////////////////////////////////////////////
    // Creates an image property
    //
    /////////////////////////////////////////////////////////////////
    function createImageProperty(property, parent) {

      var id = guid();

      var html = [

        '<div id="' + id + '" class="propertyValue">' +
        '<a href="' + property.href + '">',
        '<img src="' + property.href + '" width="128" height="128"> </img>' +
        '</a>',
        '</div>'

      ].join('\n');

      $(parent).append(html);

      return $('#' + id)[0];
    }

    /////////////////////////////////////////////////////////////////
    // Creates a file property
    //
    /////////////////////////////////////////////////////////////////
    function createFileProperty(property, parent) {

      var id = guid();

      var html = [

        '<div id="' + id + '" class="propertyValue">' +
        '<a href="' + property.href + '">',
        property.value,
        '</a>',
        '</div>'

      ].join('\n');

      $(parent).append(html);

      return $('#' + id)[0];
    }

    /////////////////////////////////////////////////////////////////
    // onPropertyClick handle
    //
    /////////////////////////////////////////////////////////////////
    _panel.onPropertyClick = function (property, event) {

      if (!property.dataType)
        return;

      switch (property.dataType) {

        case 'text':
          //nothing to do for text
          break;

        // opens link in new tab
        case 'link':
          window.open(property.href, '_blank');
          break;

        // download image or file
        case 'img':
        case 'file':
          downloadURI(property.href, property.filename);
          break;

        default:
          break;
      }
    };

    /////////////////////////////////////////////////////////////////
    // Download util
    //
    /////////////////////////////////////////////////////////////////
    function downloadURI(uri, name) {

      var link = document.createElement("a");
      link.download = name;
      link.href = uri;
      link.click();
    }

    /////////////////////////////////////////////////////////////////
    // New random guid util
    //
    /////////////////////////////////////////////////////////////////
    function guid() {

      var d = new Date().getTime();

      var guid = 'xxxx-xxxx-xxxx-xxxx-xxxx'.replace(
        /[xy]/g,
        function (c) {
          var r = (d + Math.random() * 16) % 16 | 0;
          d = Math.floor(d / 16);
          return (c == 'x' ? r : (r & 0x7 | 0x8)).toString(16);
        });

      return guid;
    };
  };

  Autodesk.ADN.Viewing.Extension.MetaPropertyPanel.prototype =
    Object.create(
      Autodesk.Viewing.Extensions.ViewerPropertyPanel.prototype);

  Autodesk.ADN.Viewing.Extension.MetaPropertyPanel.prototype.constructor =
    Autodesk.ADN.Viewing.Extension.MetaPropertyPanel;
};

Autodesk.ADN.Viewing.Extension.MetaProperties.prototype =
  Object.create(Autodesk.Viewing.Extension.prototype);

Autodesk.ADN.Viewing.Extension.MetaProperties.prototype.constructor =
  Autodesk.ADN.Viewing.Extension.MetaProperties;

Autodesk.Viewing.theExtensionManager.registerExtension(
  'Autodesk.ADN.Viewing.Extension.MetaProperties',
  Autodesk.ADN.Viewing.Extension.MetaProperties);