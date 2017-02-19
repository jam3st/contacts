/* 
    Copyright (c) 2012 - 2013 Microsoft Corporation.  All rights reserved.
    Use of this sample source code is subject to the terms of the Microsoft license 
    agreement under which you licensed this sample source code and is provided AS-IS.
    If you did not accept the terms of the license agreement, you are not authorized 
    to use this sample source code.  For the terms of the license, please see the 
    license agreement between you and Microsoft.
  
    To see all Code Samples for Windows Phone, visit http://code.msdn.microsoft.com/wpapps
  
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Windows.Storage;
using System.Xml.Linq;
using Windows.Phone.PersonalInformation;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.ApplicationModel;

namespace LocalContacts
{
    public partial class MainPage : PhoneApplicationPage
    {

        ContactStore contactStore;
        RemoteIdHelper remoteIdHelper;
        string xmlContactsFiledata;
        XDocument readDoc;

        public MainPage()
        {
            InitializeComponent();
            StatusTextBlock.Text = "Ready.";
            var version = Package.Current.Id.Version;
            StatusVersionText.Text = "Version: " + version.Major + "." + version.Minor + "." + version.Build + "." + version.Revision;

        }

        async protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (contactStore == null)
            {
                contactStore = await ContactStore.CreateOrOpenAsync(ContactStoreSystemAccessMode.ReadWrite,
                                     ContactStoreApplicationAccessMode.ReadOnly);
            }

            remoteIdHelper = new RemoteIdHelper();
            await remoteIdHelper.SetRemoteIdGuid(contactStore);
            App app = Application.Current as App;
            if(app.ReadFile != null) 
            {
                Debug.WriteLine("Deleting contacts");
                StatusTextBlock.Text = "Removing old contacts...";
                try
                {
                    await DeleteContacts();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    StatusTextBlock.Text = ex.ToString();
                    return;
                }
                StatusTextBlock.Text = "Reading " + app.ReadFile.Name + "...";
                Debug.WriteLine("Filename is " + app.ReadFile.Name);
                ImportContacts(app.ReadFile);
                if (readDoc == null)
                {
                    return;
                }
                await ParseXmlResponse(readDoc);
                StatusTextBlock.Text = "Import completed.";
                app.ReadFile = null;
                readDoc = null;
            }
            else if (app.SaveFile != null)
            {
                await GetChanges();
                CachedFileManager.DeferUpdates(app.SaveFile);
                await FileIO.WriteTextAsync(app.SaveFile, xmlContactsFiledata);
                var status = await CachedFileManager.CompleteUpdatesAsync(app.SaveFile);
                app.SaveFile = null;
                if (status == FileUpdateStatus.Complete)
                {
                    StatusTextBlock.Text = "Conacts saved.";
                }
                else
                {
                    StatusTextBlock.Text = "Contacts not saved " + status.ToString();
                }
            }
            else if(app.aborted)
            {
                StatusTextBlock.Text = "Aborted.";
            }
            else
            {
                StatusTextBlock.Text = "";
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Text = "Importing contacts...";
            var openPicker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            openPicker.FileTypeFilter.Add(".xml");
            openPicker.PickSingleFileAndContinue();
        }
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Text = "Saving contacts...";
            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "contacts.xml"
            };
            savePicker.FileTypeChoices.Add("Contacts XML file", new List<string>() { ".xml" });
            savePicker.PickSaveFileAndContinue();
        }

        /// <summary>
        /// Takes the XML representing the contacts to be inserted, updated, or deleted and creates an
        /// instance of the MyRemoteContact helper class to hold the data. Then, the helper object is passed
        /// to the function for the appropriate operation. When all of the changes are complete, 
        /// the SaveExtendedProperty helper method is called to update the last synced revision number.</summary>
        /// <param name="xmldoc">The XML document containing the contact data.</param>
        /// <returns></returns>
        private async Task ParseXmlResponse(XDocument xmldoc)
        {
            var contactElements = xmldoc.Descendants("Contact");
            uint count = 0xdeadbeef;
            foreach (var el in contactElements)
            {
                await AddContact(el, ++count);
            }
        }
        /// <summary>
        /// Adds a contact to the contact store using data supplied in the MyRemoteContact helper object.
        /// </summary>
        /// <param name="remoteContact">The MyRemoteContact helper object representing the contact to be added.</param>
        /// <returns></returns>
        public async Task AddContact(XElement el, uint id)
        {
            try
            {
                StoredContact contact = new StoredContact(contactStore);
                contact.RemoteId = await remoteIdHelper.GetTaggedRemoteId(contactStore, id.ToString());
                contact.GivenName = el.Element("GivenName").Value;
                contact.FamilyName = el.Element("FamilyName").Value;
                contact.DisplayName = el.Element("DisplayName").Value;
                IDictionary<string, object> props = await contact.GetPropertiesAsync();
                if (el.Element("AdditionalName") != null) props.Add(KnownContactProperties.AdditionalName, el.Element("AdditionalName").Value);
                if (el.Element("Address") != null) props.Add(KnownContactProperties.Address, el.Element("Address").Value);
                if (el.Element("AlternateMobileTelephone") != null) props.Add(KnownContactProperties.AlternateMobileTelephone, el.Element("AlternateMobileTelephone").Value);
                if (el.Element("Anniversary") != null) props.Add(KnownContactProperties.Anniversary, el.Element("Anniversary").Value);
                if (el.Element("Birthdate") != null) props.Add(KnownContactProperties.Birthdate, el.Element("Birthdate").Value);
                if (el.Element("Children") != null) props.Add(KnownContactProperties.Children, el.Element("Children").Value);
                if (el.Element("CompanyName") != null) props.Add(KnownContactProperties.CompanyName, el.Element("CompanyName").Value);
                if (el.Element("CompanyTelephone") != null) props.Add(KnownContactProperties.CompanyTelephone, el.Element("CompanyTelephone").Value);
    //            if (el.Element("DisplayName") != null) props.Add(KnownContactProperties.DisplayName, el.Element("DisplayName").Value);
                if (el.Element("Email") != null) props.Add(KnownContactProperties.Email, el.Element("Email").Value);
    //            if (el.Element("FamilyName") != null) props.Add(KnownContactProperties.FamilyName, el.Element("FamilyName").Value);
    //            if (el.Element("GivenName") != null) props.Add(KnownContactProperties.GivenName, el.Element("GivenName").Value);
                if (el.Element("HomeFax") != null) props.Add(KnownContactProperties.HomeFax, el.Element("HomeFax").Value);
                if (el.Element("HonorificPrefix") != null) props.Add(KnownContactProperties.HonorificPrefix, el.Element("HonorificPrefix").Value);
                if (el.Element("HonorificSuffix") != null) props.Add(KnownContactProperties.HonorificSuffix, el.Element("HonorificSuffix").Value);
                if (el.Element("JobTitle") != null) props.Add(KnownContactProperties.JobTitle, el.Element("JobTitle").Value);
                if (el.Element("Manager") != null) props.Add(KnownContactProperties.Manager, el.Element("Manager").Value);
                if (el.Element("MobileTelephone") != null) props.Add(KnownContactProperties.MobileTelephone, el.Element("MobileTelephone").Value);
                if (el.Element("Nickname") != null) props.Add(KnownContactProperties.Nickname, el.Element("Nickname").Value);
                if (el.Element("Notes") != null) props.Add(KnownContactProperties.Notes, el.Element("Notes").Value);
                if (el.Element("OfficeLocation") != null) props.Add(KnownContactProperties.OfficeLocation, el.Element("OfficeLocation").Value);
                if (el.Element("OtherAddress") != null) props.Add(KnownContactProperties.OtherAddress, el.Element("OtherAddress").Value);
                if (el.Element("OtherEmail") != null) props.Add(KnownContactProperties.OtherEmail, el.Element("OtherEmail").Value);
                if (el.Element("SignificantOther") != null) props.Add(KnownContactProperties.SignificantOther, el.Element("SignificantOther").Value);
                if (el.Element("Telephone") != null) props.Add(KnownContactProperties.Telephone, el.Element("Telephone").Value);
                if (el.Element("Url") != null) props.Add(KnownContactProperties.Url, el.Element("Url").Value);
                if (el.Element("WorkAddress") != null) props.Add(KnownContactProperties.WorkAddress, el.Element("WorkAddress").Value);
                if (el.Element("WorkEmail") != null) props.Add(KnownContactProperties.WorkEmail, el.Element("WorkEmail").Value);
                if (el.Element("WorkFax") != null) props.Add(KnownContactProperties.WorkFax, el.Element("WorkFax").Value);
                if (el.Element("WorkTelephone") != null) props.Add(KnownContactProperties.WorkTelephone, el.Element("WorkTelephone").Value);
                if (el.Element("YomiCompanyName") != null) props.Add(KnownContactProperties.YomiCompanyName, el.Element("YomiCompanyName").Value);
                if (el.Element("YomiFamilyName") != null) props.Add(KnownContactProperties.YomiFamilyName, el.Element("YomiFamilyName").Value);
                if (el.Element("YomiGivenName") != null) props.Add(KnownContactProperties.YomiGivenName, el.Element("YomiGivenName").Value);
                await contact.SaveAsync();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                StatusTextBlock.Text = e.ToString();
            }
        }
        /// <summary>
        /// Updates a contact specified the MyRemoteContact helper object from the contact store.
        /// </summary>
        /// <param name="remoteContact"></param>
        /// <returns></returns>
        public async Task DeleteContacts(ulong revision = 0)
        {
            Debug.WriteLine(String.Format("Getting changes since revision: {0}", revision));
           // Call GetChangesAsync to get all changes since the specified revision.
            var changeList = await contactStore.GetChangesAsync(revision);
            foreach (var change in changeList)
            {
                // Each change record returned contains the change type, remote and local ids, and revision number
                Debug.WriteLine(String.Format("Change Type: {0}\nLocal ID: {1}\nRemote ID: {2}\nRevision Number: {3}",
                    change.ChangeType.ToString(), 
                    change.Id,
                    await remoteIdHelper.GetUntaggedRemoteId(contactStore, change.RemoteId),
                    change.RevisionNumber));
                await contactStore.DeleteContactAsync(change.Id);
            }
        }
        /// <summary>
        /// Gets the list of changes in the local contact store since the specified revision number
        /// and generates an XML document that could be used to convey the changes to the app's web service
        /// </summary>
        /// <param name="revision">The revision number of the local store indicating which changes
        /// should be retrieved.</param>
        /// <returns></returns>
        public async Task GetChanges(ulong revision = 0)
        {
            Debug.WriteLine(String.Format("Getting changes since revision: {0}", revision));

            // Create a new XML document and add a root node.
            var doc = new XDocument();
            doc.Add(new XElement("LocalContacts"));

            // Call GetChangesAsync to get all changes since the specified revision.
            var changeList = await contactStore.GetChangesAsync(revision);

            foreach (var change in changeList)
            {
                // Each change record returned contains the change type, remote and local ids, and revision number
                Debug.WriteLine(String.Format("Change Type: {0}\nLocal ID: {1}\nRemote ID: {2}\nRevision Number: {3}",
                    change.ChangeType.ToString(),
                    change.Id,
                    await remoteIdHelper.GetUntaggedRemoteId(contactStore, change.RemoteId),
                    change.RevisionNumber));

                // Get the contact associated with the change record using the Id property.
                var contact = await contactStore.FindContactByIdAsync(change.Id);

                if (contact != null)
                {
                    // Create an xml element to represent the local contact

                    var changeElement = new XElement("Contact");
                    changeElement.Add(new XElement("RemoteId", await remoteIdHelper.GetUntaggedRemoteId(contactStore, contact.RemoteId)));
                    changeElement.Add(new XElement("GivenName", await remoteIdHelper.GetUntaggedRemoteId(contactStore, contact.GivenName)));
                    changeElement.Add(new XElement("FamilyName", await remoteIdHelper.GetUntaggedRemoteId(contactStore, contact.FamilyName)));
                    var props = await contact.GetPropertiesAsync();
                    foreach(var prop in props) {
                        if (prop.Key != null) {
                            Debug.WriteLine("Propt name " + prop.Key + " " + (string)prop.Value);
                            changeElement.Add(new XElement(prop.Key, (string)prop.Value));
                        }
                    }
                    Debug.WriteLine("Propt names done");
                    // Append the contact element to the document
                    doc.Root.Add(changeElement);
                }
            }
            xmlContactsFiledata = doc.ToString();
        }

        public async Task ImportContacts(StorageFile file)
        {
            try
            {
                var randomAccessStream = await file.OpenReadAsync();
                Stream stream = randomAccessStream.AsStreamForRead();
                readDoc = XDocument.Load(stream);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                StatusTextBlock.Text = e.ToString();
                readDoc = null;
            }
        }
    }
}
