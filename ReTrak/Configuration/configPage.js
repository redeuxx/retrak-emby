define([
  "loading",
  "emby-input",
  "emby-button",
  "emby-select",
  "emby-checkbox",
], function (loading) {
  "use strict";

  Array.prototype.remove = function () {
    var what,
      a = arguments,
      L = a.length,
      ax;
    while (L && this.length) {
      what = a[--L];
      while ((ax = this.indexOf(what)) !== -1) {
        this.splice(ax, 1);
      }
    }
    return this;
  };

  var pluginUniqueId = "4e2945d8-c6df-4613-bc75-c54d193d58ef";

  function loadConfiguration(userId, form) {
    ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
      config = config || {};
      config.ReTrakUsers = config.ReTrakUsers || [];
      var currentUserConfig = config.ReTrakUsers.filter(function (curr) {
        return curr.LinkedMbUserId == userId;
      })[0];
      
      var formElements = form.elements;
      if (!currentUserConfig) {
        currentUserConfig = {
          AccessToken: "",
          SkipUnwatchedImportFromReTrak: true,
          PostWatchedHistory: true,
          SyncCollection: true,
          ExtraLogging: false,
          ExportMediaInfo: false,
        };
      }

      currentUserConfig.LocationsExcluded =
        currentUserConfig.LocationsExcluded || [];

      formElements.txtReTrakUrl.value = config.ReTrakUrl || "https://retrak.tv";
      formElements.txtAccessToken.value = currentUserConfig.AccessToken || "";
      formElements.chkSkipUnwatchedImportFromReTrak.checked =
        currentUserConfig.SkipUnwatchedImportFromReTrak;
      formElements.chkPostWatchedHistory.checked =
        currentUserConfig.PostWatchedHistory;
      formElements.chkSyncCollection.checked = currentUserConfig.SyncCollection;
      formElements.chkExtraLogging.checked = currentUserConfig.ExtraLogging;
      formElements.chkExportMediaInfo.checked =
        currentUserConfig.ExportMediaInfo;

      ApiClient.getVirtualFolders(userId).then(function (virtualFolders) {
        loadFolders(currentUserConfig, virtualFolders, form);
      }).catch(function (err) {
        console.error("Error loading virtual folders:", err);
      }).then(function () {
        loading.hide();
      });
    }).catch(function (err) {
      console.error("Error loading configuration:", err);
      loading.hide();
    });
  }

  function populateUsers(users, userSelect) {
    userSelect.innerHTML = "";
    for (var i = 0, length = users.length; i < length; i++) {
      var user = users[i];
      var opt = document.createElement("option");
      opt.value = user.Id;
      opt.text = user.Name;
      userSelect.add(opt);
    }
  }

  function loadFolders(currentUserConfig, virtualFolders, form) {
    var retrakLocationElem = form.querySelector("#divReTrakLocations");
    if (!retrakLocationElem) return;

    // Emby may return an array or a QueryResult { Items: [...] }
    var folders = Array.isArray(virtualFolders)
      ? virtualFolders
      : (virtualFolders && virtualFolders.Items) || [];

    var html = folders.reduce(function (acc, virtualFolder) {
      acc.push(getFolderHtml(currentUserConfig, virtualFolder));
      return acc;
    }, []);
    retrakLocationElem.innerHTML = html.join("");
    
    if (typeof $ !== 'undefined' && $.fn && $.fn.trigger) {
      $(retrakLocationElem).trigger("create");
    }
  }

  function getFolderHtml(currentUserConfig, virtualFolder) {
    return (virtualFolder.Locations || []).map(function (location) {
      var isChecked = currentUserConfig.LocationsExcluded.filter(function (
        current
      ) {
        return current && current.toLowerCase() === location.toLowerCase();
      }).length;
      var checkedAttribute = isChecked ? 'checked="checked"' : "";
      return (
        '<label class="emby-checkbox-label"><input is="emby-checkbox" class="chkReTrakLocation"' +
        ' type="checkbox" data-mini="true" name="retrak_location"' +
        ' value="' + location + '" ' + checkedAttribute + " /><span>" + location + "</span></label>"
      );
    }).join("");
  }

  function onSubmit(ev) {
    ev.preventDefault();
    loading.show();

    var form = ev.currentTarget;
    var currentUserId = form.elements.selectUser.value;
    var locationsExcluded = Array.from(
      document.getElementsByName("retrak_location")
    )
      .filter(function (checkbox) {
        return checkbox.checked;
      })
      .map(function (checkbox) {
        return checkbox.value;
      });

    ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
      config = config || {};
      config.ReTrakUsers = config.ReTrakUsers || [];
      var currentUserConfig = config.ReTrakUsers.filter(function (user) {
        return user.LinkedMbUserId == currentUserId;
      })[0];
      
      if (!currentUserConfig) {
        currentUserConfig = {};
        config.ReTrakUsers.push(currentUserConfig);
      }

      currentUserConfig.SkipUnwatchedImportFromReTrak =
        form.elements.chkSkipUnwatchedImportFromReTrak.checked;
      currentUserConfig.PostWatchedHistory =
        form.elements.chkPostWatchedHistory.checked;
      currentUserConfig.SyncCollection =
        form.elements.chkSyncCollection.checked;
      currentUserConfig.ExtraLogging = form.elements.chkExtraLogging.checked;
      currentUserConfig.ExportMediaInfo =
        form.elements.chkExportMediaInfo.checked;
      currentUserConfig.AccessToken = form.elements.txtAccessToken.value;
      currentUserConfig.LinkedMbUserId = currentUserId;
      currentUserConfig.LocationsExcluded = locationsExcluded;
      config.ReTrakUrl = form.elements.txtReTrakUrl.value;

      ApiClient.updatePluginConfiguration(pluginUniqueId, config).then(
        function (result) {
          Dashboard.processPluginConfigurationUpdateResult(result);
          ApiClient.getUsers().then(function (users) {
            var currentUserId = form.elements.selectUser.value;
            populateUsers(users, form.elements.selectUser);
            form.elements.selectUser.value = currentUserId;
            loadConfiguration(currentUserId, form);
            Dashboard.alert("Settings saved.");
          }).catch(function (err) {
            console.error("Error refreshing users after save:", err);
            loading.hide();
          });
        }
      ).catch(function (err) {
        console.error("Error updating configuration:", err);
        loading.hide();
      });
    }).catch(function (err) {
      console.error("Error retrieving configuration for save:", err);
      loading.hide();
    });

    return false;
  }

  return function init(view) {
    var form = view.querySelector("#retrakConfigurationForm");
    var userSelect = form.elements.selectUser;
    form.addEventListener("submit", onSubmit);

    userSelect.addEventListener("change", function (ev) {
      loadConfiguration(ev.currentTarget.value, form);
    });

    view.addEventListener("viewshow", function () {
      loading.show();

      ApiClient.getUsers().then(function (users) {
        populateUsers(users, userSelect);
        loadConfiguration(userSelect.value, form);
      }).catch(function (err) {
        console.error("Error loading users in viewshow:", err);
        loading.hide();
      });
    });
  };
});
