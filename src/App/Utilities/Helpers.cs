﻿using Bit.App.Abstractions;
using Bit.App.Controls;
using Bit.App.Enums;
using Bit.App.Models;
using Bit.App.Models.Page;
using Bit.App.Pages;
using Bit.App.Resources;
using Plugin.Settings.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using XLabs.Ioc;

namespace Bit.App.Utilities
{
    public static class Helpers
    {
        public static readonly DateTime Epoc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static IDictionary<UriMatchType?, string> UriMatchOptionsMap = new Dictionary<UriMatchType?, string>
        {
            [UriMatchType.Domain] = AppResources.BaseDomain,
            [UriMatchType.Host] = AppResources.Host,
            [UriMatchType.StartsWith] = AppResources.StartsWith,
            [UriMatchType.RegularExpression] = AppResources.RegEx,
            [UriMatchType.Exact] = AppResources.Exact,
            [UriMatchType.Never] = AppResources.Never
        };

        public static long EpocUtcNow()
        {
            return (long)(DateTime.UtcNow - Epoc).TotalMilliseconds;
        }

        public static T OnPlatform<T>(T iOS = default(T), T Android = default(T),
            T WinPhone = default(T), T Windows = default(T), string platform = null)
        {
            if(platform == null)
            {
                platform = Device.RuntimePlatform;
            }

            switch(platform)
            {
                case Device.iOS:
                    return iOS;
                case Device.Android:
                    return Android;
                case Device.WinPhone:
                    return WinPhone;
                case Device.UWP:
                    return Windows;
                default:
                    throw new Exception("Unsupported platform.");
            }
        }

        public static bool InDebugMode()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }

        public static bool PerformUpdateTasks(ISettings settings,
            IAppInfoService appInfoService, IDatabaseService databaseService, ISyncService syncService)
        {
            var lastBuild = settings.GetValueOrDefault(Constants.LastBuildKey, null);
            if(InDebugMode() || lastBuild == null || lastBuild != appInfoService.Build)
            {
                settings.AddOrUpdateValue(Constants.LastBuildKey, appInfoService.Build);
                databaseService.CreateTables();
                var task = Task.Run(async () => await syncService.FullSyncAsync(true));
                return true;
            }

            return false;
        }

        public static string GetEmptyTableSectionTitle()
        {
            if(Device.RuntimePlatform == Device.iOS)
            {
                return string.Empty;
            }

            return " ";
        }

        public static string ToolbarImage(string image)
        {
            if(Device.RuntimePlatform == Device.iOS || Device.RuntimePlatform == Device.Android)
            {
                return null;
            }

            return image;
        }

        public static async void CipherMoreClickedAsync(Page page, VaultListPageModel.Cipher cipher, bool autofill)
        {
            var buttons = new List<string> { AppResources.View, AppResources.Edit };

            if(cipher.Type == CipherType.Login)
            {
                if(!string.IsNullOrWhiteSpace(cipher.LoginPassword.Value))
                {
                    buttons.Add(AppResources.CopyPassword);
                }
                if(!string.IsNullOrWhiteSpace(cipher.LoginUsername))
                {
                    buttons.Add(AppResources.CopyUsername);
                }
                if(!autofill && !string.IsNullOrWhiteSpace(cipher.LoginUri) && (cipher.LoginUri.StartsWith("http://")
                    || cipher.LoginUri.StartsWith("https://")))
                {
                    buttons.Add(AppResources.GoToWebsite);
                }
            }
            else if(cipher.Type == CipherType.Card)
            {
                if(!string.IsNullOrWhiteSpace(cipher.CardNumber))
                {
                    buttons.Add(AppResources.CopyNumber);
                }
                if(!string.IsNullOrWhiteSpace(cipher.CardCode.Value))
                {
                    buttons.Add(AppResources.CopySecurityCode);
                }
            }

            var selection = await page.DisplayActionSheet(cipher.Name, AppResources.Cancel, null, buttons.ToArray());

            if(selection == AppResources.View)
            {
                var p = new VaultViewCipherPage(cipher.Type, cipher.Id);
                await page.Navigation.PushForDeviceAsync(p);
            }
            else if(selection == AppResources.Edit)
            {
                var p = new VaultEditCipherPage(cipher.Id);
                await page.Navigation.PushForDeviceAsync(p);
            }
            else if(selection == AppResources.CopyPassword)
            {
                CipherCopy(cipher.LoginPassword.Value, AppResources.Password);
            }
            else if(selection == AppResources.CopyUsername)
            {
                CipherCopy(cipher.LoginUsername, AppResources.Username);
            }
            else if(selection == AppResources.GoToWebsite)
            {
                Device.OpenUri(new Uri(cipher.LoginUri));
            }
            else if(selection == AppResources.CopyNumber)
            {
                CipherCopy(cipher.CardNumber, AppResources.Number);
            }
            else if(selection == AppResources.CopySecurityCode)
            {
                CipherCopy(cipher.CardCode.Value, AppResources.SecurityCode);
            }
        }

        public static void CipherCopy(string copyText, string alertLabel)
        {
            var daService = Resolver.Resolve<IDeviceActionService>();
            daService.CopyToClipboard(copyText);
            daService.Toast(string.Format(AppResources.ValueHasBeenCopied, alertLabel));
        }

        public static async void AddCipher(Page page, string folderId)
        {
            var type = await page.DisplayActionSheet(AppResources.SelectTypeAdd, AppResources.Cancel, null,
                AppResources.TypeLogin, AppResources.TypeCard, AppResources.TypeIdentity, AppResources.TypeSecureNote);

            var selectedType = CipherType.SecureNote;
            if(type == null || type == AppResources.Cancel)
            {
                return;
            }
            else if(type == AppResources.TypeLogin)
            {
                selectedType = CipherType.Login;
            }
            else if(type == AppResources.TypeCard)
            {
                selectedType = CipherType.Card;
            }
            else if(type == AppResources.TypeIdentity)
            {
                selectedType = CipherType.Identity;
            }
            else if(type == AppResources.TypeSecureNote)
            {
                selectedType = CipherType.SecureNote;
            }
            else
            {
                return;
            }

            var addPage = new VaultAddCipherPage(selectedType, defaultFolderId: folderId);
            await page.Navigation.PushForDeviceAsync(addPage);
        }

        public static async Task AddField(Page page, TableSection fieldsSection)
        {
            var type = await page.DisplayActionSheet(AppResources.SelectTypeField, AppResources.Cancel, null,
                AppResources.FieldTypeText, AppResources.FieldTypeHidden, AppResources.FieldTypeBoolean);

            FieldType fieldType;
            if(type == AppResources.FieldTypeText)
            {
                fieldType = FieldType.Text;
            }
            else if(type == AppResources.FieldTypeHidden)
            {
                fieldType = FieldType.Hidden;
            }
            else if(type == AppResources.FieldTypeBoolean)
            {
                fieldType = FieldType.Boolean;
            }
            else
            {
                return;
            }

            var daService = Resolver.Resolve<IDeviceActionService>();
            var label = await daService.DisplayPromptAync(AppResources.CustomFieldName);
            if(label == null)
            {
                return;
            }

            var cell = MakeFieldCell(fieldType, label, string.Empty, fieldsSection);
            if(cell != null)
            {
                fieldsSection.Insert(fieldsSection.Count - 1, cell);
                if(cell is FormEntryCell feCell)
                {
                    feCell.InitEvents();
                }
            }
        }

        public static Cell MakeFieldCell(FieldType type, string label, string value, TableSection fieldsSection)
        {
            Cell cell;
            switch(type)
            {
                case FieldType.Text:
                case FieldType.Hidden:
                    var hidden = type == FieldType.Hidden;
                    var textFieldCell = new FormEntryCell(label, isPassword: hidden,
                        button1: hidden ? "eye.png" : null);
                    textFieldCell.Entry.Text = value;
                    textFieldCell.Entry.DisableAutocapitalize = true;
                    textFieldCell.Entry.Autocorrect = false;

                    if(hidden)
                    {
                        textFieldCell.Entry.FontFamily = Helpers.OnPlatform(
                            iOS: "Menlo-Regular", Android: "monospace", Windows: "Courier");
                        textFieldCell.Button1.Command = new Command(() =>
                        {
                            textFieldCell.Entry.InvokeToggleIsPassword();
                            textFieldCell.Button1.Image =
                                "eye" + (!textFieldCell.Entry.IsPasswordFromToggled ? "_slash" : string.Empty) + ".png";
                        });
                    }
                    cell = textFieldCell;
                    break;
                case FieldType.Boolean:
                    var switchFieldCell = new ExtendedSwitchCell
                    {
                        Text = label,
                        On = value == "true"
                    };
                    cell = switchFieldCell;
                    break;
                default:
                    cell = null;
                    break;
            }

            if(cell != null)
            {
                var deleteAction = new MenuItem { Text = AppResources.Remove, IsDestructive = true };
                deleteAction.Clicked += (sender, e) =>
                {
                    if(fieldsSection.Contains(cell))
                    {
                        fieldsSection.Remove(cell);
                    }

                    if(cell is FormEntryCell feCell)
                    {
                        feCell.Dispose();
                    }
                    cell = null;
                };

                var editNameAction = new MenuItem { Text = AppResources.Edit };
                editNameAction.Clicked += async (sender, e) =>
                {
                    string existingLabel = null;
                    var feCell = cell as FormEntryCell;
                    var esCell = cell as ExtendedSwitchCell;
                    if(feCell != null)
                    {
                        existingLabel = feCell.Label.Text;
                    }
                    else if(esCell != null)
                    {
                        existingLabel = esCell.Text;
                    }

                    var daService = Resolver.Resolve<IDeviceActionService>();
                    var editLabel = await daService.DisplayPromptAync(AppResources.CustomFieldName,
                        null, existingLabel);
                    if(editLabel != null)
                    {
                        if(feCell != null)
                        {
                            feCell.Label.Text = editLabel;
                        }
                        else if(esCell != null)
                        {
                            esCell.Text = editLabel;
                        }
                    }
                };

                cell.ContextActions.Add(editNameAction);
                cell.ContextActions.Add(deleteAction);
            }

            return cell;
        }

        public static void ProcessFieldsSectionForSave(TableSection fieldsSection, Cipher cipher)
        {
            if(fieldsSection != null && fieldsSection.Count > 0)
            {
                var fields = new List<Field>();
                foreach(var cell in fieldsSection)
                {
                    if(cell is FormEntryCell entryCell)
                    {
                        fields.Add(new Field
                        {
                            Name = string.IsNullOrWhiteSpace(entryCell.Label.Text) ? null :
                                entryCell.Label.Text.Encrypt(cipher.OrganizationId),
                            Value = string.IsNullOrWhiteSpace(entryCell.Entry.Text) ? null :
                                entryCell.Entry.Text.Encrypt(cipher.OrganizationId),
                            Type = entryCell.Entry.IsPassword ? FieldType.Hidden : FieldType.Text
                        });
                    }
                    else if(cell is ExtendedSwitchCell switchCell)
                    {
                        var value = switchCell.On ? "true" : "false";
                        fields.Add(new Field
                        {
                            Name = string.IsNullOrWhiteSpace(switchCell.Text) ? null :
                                switchCell.Text.Encrypt(cipher.OrganizationId),
                            Value = value.Encrypt(cipher.OrganizationId),
                            Type = FieldType.Boolean
                        });
                    }
                }
                cipher.Fields = fields;
            }

            if(!cipher.Fields?.Any() ?? true)
            {
                cipher.Fields = null;
            }
        }

        public static FormEntryCell MakeUriCell(string value, UriMatchType? match, TableSection urisSection, Page page)
        {
            var label = string.Format(AppResources.URIPosition, urisSection.Count);
            var cell = new FormEntryCell(label, entryKeyboard: Keyboard.Url);
            cell.Entry.Text = value;
            cell.Entry.DisableAutocapitalize = true;
            cell.Entry.Autocorrect = false;
            cell.MetaData = new Dictionary<string, object> { ["match"] = match };

            var deleteAction = new MenuItem { Text = AppResources.Remove, IsDestructive = true };
            deleteAction.Clicked += (sender, e) =>
            {
                if(urisSection.Contains(cell))
                {
                    urisSection.Remove(cell);
                    if(cell is FormEntryCell feCell)
                    {
                        feCell.Dispose();
                    }
                    cell = null;

                    for(int i = 0; i < urisSection.Count; i++)
                    {
                        if(urisSection[i] is FormEntryCell uriCell)
                        {
                            uriCell.Label.Text = string.Format(AppResources.URIPosition, i + 1);
                        }
                    }
                }
            };

            var optionsAction = new MenuItem { Text = AppResources.Options };
            optionsAction.Clicked += async (sender, e) =>
            {
                var options = UriMatchOptionsMap.Select(v => v.Value).ToList();
                options.Insert(0, AppResources.Default);
                var exactingMatchVal = cell.MetaData["match"] as UriMatchType?;

                var matchIndex = exactingMatchVal.HasValue ?
                    Array.IndexOf(UriMatchOptionsMap.Keys.ToArray(), exactingMatchVal) + 1 : 0;
                options[matchIndex] = $"✓ {options[matchIndex]}";

                var optionsArr = options.ToArray();
                var val = await page.DisplayActionSheet(AppResources.URIMatchDetection, AppResources.Cancel,
                    null, options.ToArray());

                UriMatchType? selectedVal = null;
                if(val == AppResources.Cancel)
                {
                    selectedVal = exactingMatchVal;
                }
                else if(val != AppResources.Default)
                {
                    selectedVal = UriMatchOptionsMap.ElementAt(Array.IndexOf(optionsArr, val) - 1).Key;
                }
                cell.MetaData["match"] = selectedVal;
            };

            cell.ContextActions.Add(optionsAction);
            cell.ContextActions.Add(deleteAction);
            return cell;
        }

        public static void ProcessUrisSectionForSave(TableSection urisSection, Cipher cipher)
        {
            if(urisSection != null && urisSection.Count > 0)
            {
                var uris = new List<LoginUri>();
                foreach(var cell in urisSection)
                {
                    if(cell is FormEntryCell entryCell && !string.IsNullOrWhiteSpace(entryCell.Entry.Text))
                    {
                        var match = entryCell?.MetaData["match"] as UriMatchType?;
                        uris.Add(new LoginUri
                        {
                            Uri = entryCell.Entry.Text.Encrypt(cipher.OrganizationId),
                            Match = match
                        });
                    }
                }
                cipher.Login.Uris = uris;
            }

            if(!cipher.Login.Uris?.Any() ?? true)
            {
                cipher.Login.Uris = null;
            }
        }

        public static void InitSectionEvents(TableSection section)
        {
            if(section != null && section.Count > 0)
            {
                foreach(var cell in section)
                {
                    if(cell is FormEntryCell entrycell)
                    {
                        entrycell.InitEvents();
                    }
                }
            }
        }

        public static void DisposeSectionEvents(TableSection section)
        {
            if(section != null && section.Count > 0)
            {
                foreach(var cell in section)
                {
                    if(cell is FormEntryCell entrycell)
                    {
                        entrycell.Dispose();
                    }
                }
            }
        }

        public static string GetUrlHost(string url)
        {
            if(string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            url = url.Trim();
            if(url == string.Empty)
            {
                return null;
            }

            if(!url.Contains("://"))
            {
                url = $"http://{url}";
            }

            if(!Uri.TryCreate(url, UriKind.Absolute, out Uri u))
            {
                return null;
            }

            var host = u.Host;
            if(!u.IsDefaultPort)
            {
                host = $"{host}:{u.Port}";
            }

            return host;
        }
    }
}
