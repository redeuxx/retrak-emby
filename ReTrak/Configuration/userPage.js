define([
  "loading",
  "emby-input",
  "emby-button",
  "emby-checkbox",
], function (loading) {
  "use strict";

  var pluginUniqueId = "8abc6789-fde2-4705-8592-4028806fa343";

  function getCurrentUserId() {
    // ApiClient.getCurrentUserId() gives the logged-in user's ID
    return ApiClient.getCurrentUserId();
  }

  function loadUserConfiguration(userId, form) {
    ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
      var currentUserConfig = (config.ReTrakUsers || []).filter(function (u) {
        return u.LinkedMbUserId == userId;
      })[0];

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

      var formElements = form.elements;
      formElements.txtUserAccessToken.value = currentUserConfig.AccessToken || "";
      formElements.chkUserSkipUnwatchedImportFromReTrak.checked = currentUserConfig.SkipUnwatchedImportFromReTrak;
      formElements.chkUserPostWatchedHistory.checked = currentUserConfig.PostWatchedHistory;
      formElements.chkUserSyncCollection.checked = currentUserConfig.SyncCollection;
      formElements.chkUserExtraLogging.checked = currentUserConfig.ExtraLogging;
      formElements.chkUserExportMediaInfo.checked = currentUserConfig.ExportMediaInfo;

      loading.hide();
    });
  }

  function onSubmit(ev) {
    ev.preventDefault();
    loading.show();

    var form = ev.currentTarget;
    var userId = getCurrentUserId();

    ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
      config.ReTrakUsers = config.ReTrakUsers || [];

      var currentUserConfig = config.ReTrakUsers.filter(function (u) {
        return u.LinkedMbUserId == userId;
      })[0];

      if (!currentUserConfig) {
        currentUserConfig = { LinkedMbUserId: userId };
        config.ReTrakUsers.push(currentUserConfig);
      }

      currentUserConfig.AccessToken = form.elements.txtUserAccessToken.value;
      currentUserConfig.SkipUnwatchedImportFromReTrak = form.elements.chkUserSkipUnwatchedImportFromReTrak.checked;
      currentUserConfig.PostWatchedHistory = form.elements.chkUserPostWatchedHistory.checked;
      currentUserConfig.SyncCollection = form.elements.chkUserSyncCollection.checked;
      currentUserConfig.ExtraLogging = form.elements.chkUserExtraLogging.checked;
      currentUserConfig.ExportMediaInfo = form.elements.chkUserExportMediaInfo.checked;
      currentUserConfig.LinkedMbUserId = userId;

      ApiClient.updatePluginConfiguration(pluginUniqueId, config).then(function (result) {
        Dashboard.processPluginConfigurationUpdateResult(result);
        Dashboard.alert("Settings saved.");
        loading.hide();
      });
    });

    return false;
  }

  return function init(view) {
    var form = view.querySelector("#retrakUserConfigurationForm");
    form.addEventListener("submit", onSubmit);

    view.addEventListener("viewshow", function () {
      loading.show();
      var userId = getCurrentUserId();
      loadUserConfiguration(userId, form);
    });
  };
});
