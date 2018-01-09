define(["sitecore", "jquery"], function (Sitecore) {
    var Database_operations = Sitecore.Definitions.App.extend({
        initialized: function () {
            app = this;
            //todo add wait indicator
            this.getStatus();
            if (!this.ProgressMessageBar.viewModel.hasMessages()) {
                this.Indicator.viewModel.IsBusy = true;
                this.setRange();
                this.setContactsCount();
                this.setDevicesCount();
                this.setUserAgentsCount();
                this.formDataExists();
                this.Indicator.viewModel.IsBusy = false;
                window.setInterval(this.getStatus, 3000);
            }

            //Replace all [br]-tokens with actual breaks.
            jQuery(".sc-text").each(function () {
                var html = jQuery(this).html();
                html = html.replace(/\[br\]/g, '<br/>');
                html = html.replace(/\[b\]/g, '<b>');
                html = html.replace(/\[\/b\]/g, '</b>');
                jQuery(this).html(html);
            });
        },
        haltAllJobs: function () {
            jQuery.ajax({
                type: "POST",
                dataType: "json",
                url: "/sitecore/ADM/Operations/StopAllJobs",
                cache: false,
                success: function (data) {
                    app.getStatus();
                },
                error: function (data) {
                    app.ProgressMessageBar.addMessage("eror", "Error while halting all jobs");
                }
            });

        },
        clearData: function () {
            app.ProgressMessageBar.removeMessages();
            jQuery.ajax({
                type: "POST",
                dataType: "json",
                url: "/sitecore/ADM/Operations/startClearing",
                data: {
                    'endDate': this.DatePicker1.viewModel.getDate().toISOString(),
                    'startDate': this.DatePickerFrom.viewModel.getDate().toISOString(),
                    'filterInteractions': this.FilterInteractions.get("isChecked"),
                    "removeContacts": this.RemoveContacts.get("isChecked"),
                    "filterContacts": this.FilterContacts.get("isChecked"),
                    "removeFormData": this.RemoveFormData.get("isChecked"),
                    "removeUserAgents": this.RemoveUserAgents.get("isChecked"),
                    "removeDevices": this.RemoveDevices.get("isChecked"),
                    "removeRobotsOnly": this.RemoveRobotsOnly.get("isChecked")
                },
                success: function (data) {
                    app.RemoveButton.viewModel.disable();
                    app.getStatus();
                },
                error: function (data) {
                    app.ProgressMessageBar.addMessage("error", "Error while starting the clearing");
                }

            });
        },
        clearContactData: function () {
            app.ProgressMessageBarWI.removeMessages();
            jQuery.ajax({
                type: "POST",
                dataType: "json",
                url: "/sitecore/ADM/Operations/removeContacts",
                data: { "filterContacts": this.RemoveIdentifiedContactsWI.get("isChecked") },
                success: function (data) {
                    app.RemoveContactsWIButton.viewModel.disable();
                    app.getStatus();
                },
                error: function (data) {
                    app.ProgressMessageBarWI.addMessage("eror", "Error while starting the clearing contact data");
                }

            });
        },

        clearDevicesData: function () {
            app.ProgressMessageBarDevice.removeMessages();
            jQuery.ajax({
                type: "POST",
                dataType: "json",
                url: "/sitecore/ADM/Operations/removeDevices",
                success: function (data) {
                    app.RemoveDevicesButton.viewModel.disable();
                    app.getStatus();
                },
                error: function (data) {
                    app.ProgressMessageBarDevice.addMessage("eror", "Error while starting clearing devices");
                }
            });
        },

        clearUserAgentData: function () {
            app.ProgressMessageBarUA.removeMessages();
            jQuery.ajax({
                type: "POST",
                dataType: "json",
                url: "/sitecore/ADM/Operations/removeUserAgents",
                success: function (data) {
                    app.RemoveUserAgentsButton.viewModel.disable();
                    app.getStatus();
                },
                error: function (data) {
                    app.ProgressMessageBarUA.addMessage("eror", "Error while starting clearing userAgents");
                }
            });
        },

        clearRobotUserAgentData: function () {
            app.ProgressMessageBarUA.removeMessages();
            jQuery.ajax({
                type: "POST",
                dataType: "json",
                url: "/sitecore/ADM/Operations/RemoveRobotUserAgents",
                success: function (data) {
                    app.RemoveRobotUserAgentsButton.viewModel.disable();
                    app.getStatus();
                },
                error: function (data) {
                    app.ProgressMessageBarUA.addMessage("eror", "Error while starting clearing robot userAgents");
                }
            });
        },

        indexContacts: function () {
            app.ProgressMessageBarIndex.removeMessages();
            jQuery.ajax({
                type: "POST",
                dataType: "json",
                url: "/sitecore/ADM/Operations/indexContacts",
                success: function (data) {
                    app.IndexContactsButton.viewModel.disable();
                    app.getStatus();
                },
                error: function (data) {
                    app.ProgressMessageBarIndex
                        .addMessage("eror", "Error while starting the clearing contact data");
                }

            });
        },
        formDataExists: function () {
            app.ProgressMessageBar.removeMessages();
            jQuery.ajax({
                type: "POST",
                dataType: "json",
                url: "/sitecore/ADM/Operations/formDataExists",
                data: {},
                success: function (data) {
                    if (!data.Data.exists) {
                        app.RemoveFormData.viewModel.hide();
                    }
                },
                error: function (data) {
                    app.ProgressMessageBar.addMessage("eror", "Error while getting form data status");
                }

            });
        },
        getStatus: function () {
            jQuery.ajax({
                type: "GET",
                dataType: "json",
                url: "/sitecore/ADM/Operations/getstatus",
                cache: false,
                success: function (data) {
                    app.setDatabaseStatusData(data.Data.databaseJob);
                    app.setContactsStatusData(data.Data.contactsJob);
                    app.setDevicesStatusData(data.Data.devicesJob);
                    app.setUserAgentsStatusData(data.Data.useragentsJob);
                    app.setIndexContactsStatusData(data.Data.indexJob);
                },
                error: function (data) {
                    if (data.status === 401) {
                        app.TabControl1.viewModel.hide(true);
                        app.GlobalMessageBar.removeMessages();
                        app.GlobalMessageBar.addMessage("warning", "You don't have permissions to use this application. Please contact you administrator.");
                    } else {
                        app.ProgressMessageBar.addMessage("error", "Getting progress failed!");
                    }
                }
            });
        },

        setDatabaseStatusData: function (data) {
            if (data.messages.length == 0)
                return;

            app.ProgressMessageBar.removeMessages();
            for (var i = 0; i < data.messages.length; i++)
                app.ProgressMessageBar.addMessage("notification", data.messages[i]);
            if (!(data.messages.length < 3 || data.messages[data.messages.length - 1].startsWith("Job ended:")))
                app.ProgressMessageBar.addMessage("notification", data.processed + "/" + data.total + " items were processed");

            if (data.isRunning) {
                app.RemoveButton.viewModel.disable();
                app.DatePicker1.viewModel.disabled(true);
                app.DatePickerFrom.viewModel.disabled(true);
            } else if (!app.RemoveButton.viewModel.isEnabled()) {
                app.RemoveButton.viewModel.enable();
                app.DatePicker1.viewModel.disabled(false);
                app.DatePickerFrom.viewModel.disabled(false);
                //app.setRange();
            }
        },

        setContactsStatusData: function (data) {
            if (data.messages.length == 0)
                return;

            app.ProgressMessageBarWI.removeMessages();
            for (var i = 0; i < data.messages.length; i++)
                app.ProgressMessageBarWI.addMessage("notification", data.messages[i]);
            if (!(data.messages.length < 2 || data.messages[data.messages.length - 1].startsWith("Job ended:")))
                app.ProgressMessageBarWI.addMessage("notification", data.processed + "/" + data.total + " items were processed");

            if (data.isRunning) {
                app.RemoveContactsWIButton.viewModel.disable();
                //app.DatePicker1.viewModel.disable();
            } else if (!app.RemoveContactsWIButton.viewModel.isEnabled()) {
                app.RemoveContactsWIButton.viewModel.enable();
            }
        },

        setDevicesStatusData: function (data) {
            if (data.messages.length == 0)
                return;

            app.ProgressMessageBarDevice.removeMessages();
            for (var i = 0; i < data.messages.length; i++)
                app.ProgressMessageBarDevice.addMessage("notification", data.messages[i]);
            if (!(data.messages.length < 2 || data.messages[data.messages.length - 1].startsWith("Job ended:")))
                app.ProgressMessageBarDevice.addMessage("notification", data.processed + "/" + data.total + " items were processed");

            if (data.isRunning) {
                app.RemoveDevicesButton.viewModel.disable();
                //app.DatePicker1.viewModel.disable();
            } else if (!app.RemoveDevicesButton.viewModel.isEnabled()) {
                app.RemoveDevicesButton.viewModel.enable();
            }
        },

        setUserAgentsStatusData: function (data) {
            if (data.messages.length == 0)
                return;

            app.ProgressMessageBarUA.removeMessages();
            for (var i = 0; i < data.messages.length; i++)
                app.ProgressMessageBarUA.addMessage("notification", data.messages[i]);
            if (!(data.messages.length < 2 || data.messages[data.messages.length - 1].startsWith("Job ended:")))
                app.ProgressMessageBarUA.addMessage("notification", data.processed + "/" + data.total + " items were processed");

            if (data.isRunning) {
                app.RemoveUserAgentsButton.viewModel.disable();
                app.RemoveRobotUserAgentsButton.viewModel.disable();
                //app.DatePicker1.viewModel.disable();
            } else if (!app.RemoveUserAgentsButton.viewModel.isEnabled()) {
                app.RemoveUserAgentsButton.viewModel.enable();
                app.RemoveRobotUserAgentsButton.viewModel.enable();
            }
        },

        setIndexContactsStatusData: function (data) {
            if (data.messages.length == 0)
                return;

            app.ProgressMessageBarIndex.removeMessages();
            for (var i = 0; i < data.messages.length; i++)
                app.ProgressMessageBarIndex.addMessage("notification", data.messages[i]);
            if (!(data.messages.length < 2 || data.messages[data.messages.length - 1].startsWith("Job ended:")))
                app.ProgressMessageBarIndex.addMessage("notification", data.processed + " items were put in the processing pool");
            if (data.inPool !== 0) {
                app.ProgressMessageBarIndex.addMessage("notification", "There are " + data.inPool + " contacts in the processing pool.");
            }
            if (data.isRunning) {
                app.IndexContactsButton.viewModel.disable();
                //app.DatePicker1.viewModel.disable();
            } else if (!app.IndexContactsButton.viewModel.isEnabled()) {
                app.IndexContactsButton.viewModel.enable();
            }
        },
        setContactsCount: function () {
            jQuery.ajax({
                type: "GET",
                dataType: "json",
                url: "/sitecore/ADM/Operations/getContactsCount",
                cache: false,
                success: function (data) {
                    app.ContactsCount.set("text", "There are " + data.Data.count + " contacts in the database");
                    if (data.Data.count === 0) {
                        app.RemoveContactsWIButton.viewModel.disable();
                    }
                },
                error: function (data) {
                    app.ProgressMessageBarWI.addMessage("error", "Getting contacts count failed!");
                }
            });
        },
        setDevicesCount: function () {
            jQuery.ajax({
                type: "GET",
                dataType: "json",
                url: "/sitecore/ADM/Operations/getDevicesCount",
                cache: false,
                success: function (data) {
                    app.DevicesCount.set("text", "There are " + data.Data.count + " devices in the database");
                    if (data.Data.count === 0) {
                        app.RemoveDevicesButton.viewModel.disable();
                    }
                },
                error: function (data) {
                    app.ProgressMessageBarDevice.addMessage("error", "Getting devices count failed!");
                }
            });
        },
        setUserAgentsCount: function () {
            jQuery.ajax({
                type: "GET",
                dataType: "json",
                url: "/sitecore/ADM/Operations/getUserAgentsCount",
                cache: false,
                success: function (data) {
                    app.UserAgentsCount.set("text", "There are " + data.Data.count + " userAgents in the database");
                    if (data.Data.count === 0) {
                        app.RemoveUserAgentsButton.viewModel.disable();
                    }
                },
                error: function (data) {
                    app.ProgressMessageBarUA.addMessage("error", "Getting userAgents count failed!");
                }
            });
        },

        setRange: function () {
            jQuery.ajax({
                type: "GET",
                dataType: "json",
                url: "/sitecore/ADM/Operations/getRange",
                data: {},
                cache: false,
                success: function (data) {
                    if (data.Data.startDate == null || data.Data.endDate == null) {
                        app.RangeInfo.set("text", "Interactions were not found in the analytics database.");
                        app.DatePicker1.viewModel.disabled(true);
                        app.DatePickerFrom.viewModel.disabled(true);
                        return;
                    }
                    var startDate = new Date(data.Data.startDate);
                    var endDate = new Date(data.Data.endDate);
                    app.RangeInfo.set("text",
                        "There are " + data.Data.count + " interactions available in the database for the following date range: " +
                        startDate.getUTCDate().toString() + "/" + (startDate.getUTCMonth() + 1).toString() + "/" + startDate.getUTCFullYear().toString() +
                        " - " +
                        endDate.getUTCDate().toString() + "/" + (endDate.getUTCMonth() + 1).toString() + "/" + endDate.getUTCFullYear().toString());
                    endDate = new Date(endDate.setTime(endDate.getTime() + 86400000)); //one day is added.
                    app.DatePicker1.viewModel.$el.datepicker("option", "maxDate", endDate);
                    app.DatePicker1.viewModel.$el.datepicker("option", "minDate", startDate);
                    app.DatePickerFrom.viewModel.$el.datepicker("option", "maxDate", endDate);
                    app.DatePickerFrom.viewModel.$el.datepicker("option", "minDate", startDate);
                },
                error: function (data) {
                    if (data.status === 401) {
                        app.ProgressMessageBar.removeMessages();
                        app.ProgressMessageBar.addMessage("error", "Access to application is denied");
                    } else {
                        app.ProgressMessageBar.addMessage("error", "Error while getting range");
                    }
                }
            });
        }
    });

    return Database_operations;
});