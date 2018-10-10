using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Users;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;

namespace Unigram.ViewModels.Gallery
{
    public abstract class GalleryViewModelBase : TLViewModelBase/*, IHandle<UpdateFile>*/
    {
        public IFileDelegate Delegate { get; set; }

        public GalleryViewModelBase(IProtoService protoService, IEventAggregator aggregator)
            : base(protoService, null, null, aggregator)
        {
            StickersCommand = new RelayCommand(StickersExecute);
            ViewCommand = new RelayCommand(ViewExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
            CopyCommand = new RelayCommand(CopyExecute);
            SaveCommand = new RelayCommand(SaveExecute);
            OpenWithCommand = new RelayCommand(OpenWithExecute);

            //Aggregator.Subscribe(this);
        }

        //public void Handle(UpdateFile update)
        //{
        //    BeginOnUIThread(() => Delegate?.UpdateFile(update.File));
        //}

        //protected override void BeginOnUIThread(Action action)
        //{
        //    // This is somehow needed because this viewmodel requires a Dispatcher
        //    // in some situations where base one might be null.
        //    Execute.BeginOnUIThread(action);
        //}

        public virtual int Position
        {
            get
            {
                return SelectedIndex + 1;
            }
        }

        public int SelectedIndex
        {
            get
            {
                if (Items == null || SelectedItem == null)
                {
                    return 0;
                }

                var index = Items.IndexOf(SelectedItem);
                if (Items.Count > 1)
                {
                    if (index == Items.Count - 1)
                    {
                        LoadNext();
                    }
                    if (index == 0)
                    {
                        LoadPrevious();
                    }
                }

                return index;
            }
        }

        protected int _totalItems;
        public int TotalItems
        {
            get
            {
                return _totalItems;
            }
            set
            {
                Set(ref _totalItems, value);
            }
        }

        protected GalleryItem _selectedItem;
        public GalleryItem SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
                OnSelectedItemChanged(value);
                //RaisePropertyChanged(() => SelectedIndex);
                RaisePropertyChanged(() => Position);
            }
        }

        protected GalleryItem _firstItem;
        public GalleryItem FirstItem
        {
            get
            {
                return _firstItem;
            }
            set
            {
                Set(ref _firstItem, value);
            }
        }

        protected object _poster;
        public object Poster
        {
            get
            {
                return _poster;
            }
            set
            {
                Set(ref _poster, value);
            }
        }

        public MvxObservableCollection<GalleryItem> Items { get; protected set; }

        public virtual MvxObservableCollection<GalleryItem> Group { get; }

        protected virtual void LoadPrevious() { }

        protected virtual void LoadNext() { }

        protected virtual void OnSelectedItemChanged(GalleryItem item) { }

        public virtual bool CanDelete
        {
            get
            {
                return false;
            }
        }

        public virtual bool CanOpenWith
        {
            get
            {
                if (SelectedItem is GalleryMessageItem message && message.IsHot)
                {
                    return false;
                }

                return true;
            }
        }

        public RelayCommand StickersCommand { get; }
        private async void StickersExecute()
        {
            if (_selectedItem != null && _selectedItem.HasStickers)
            {
                var file = _selectedItem.GetFile();
                if (file == null)
                {
                    return;
                }

                var response = await ProtoService.SendAsync(new GetAttachedStickerSets(file.Id));
                if (response is StickerSets sets)
                {
                    if (sets.Sets.Count > 1)
                    {
                        await AttachedStickersView.GetForCurrentView().ShowAsync(sets.Sets);
                    }
                    else if (sets.Sets.Count > 0)
                    {
                        await StickerSetView.GetForCurrentView().ShowAsync(sets.Sets[0].Id);
                    }
                }
            }
        }

        public RelayCommand ViewCommand { get; }
        protected virtual void ViewExecute()
        {
            NavigationService.GoBack();

            var message = _selectedItem as GalleryMessageItem;
            if (message == null)
            {
                return;
            }

            var service = WindowContext.GetForCurrentView().NavigationServices.GetByFrameId("Main" + ProtoService.SessionId);
            if (service != null)
            {
                service.NavigateToChat(message.ChatId, message: message.Id);
            }
        }

        public RelayCommand DeleteCommand { get; }
        protected virtual void DeleteExecute()
        {
        }

        public RelayCommand CopyCommand { get; }
        protected async void CopyExecute()
        {
            var item = _selectedItem;
            if (item == null)
            {
                return;
            }

            var file = item.GetFile();

            if (file.Local.IsDownloadingCompleted)
            {
                try
                {
                    var temp = await StorageFile.GetFileFromPathAsync(file.Local.Path);

                    var dataPackage = new DataPackage();
                    dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(temp));
                    ClipboardEx.TrySetContent(dataPackage);
                }
                catch { }
            }
        }

        public RelayCommand SaveCommand { get; }
        protected virtual async void SaveExecute()
        {
            var item = _selectedItem;
            if (item == null)
            {
                return;
            }

            var result = item.GetFileAndName();

            var file = result.File;
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var fileName = result.FileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = System.IO.Path.GetFileName(file.Local.Path);
            }

            var extension = System.IO.Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".dat";
            }

            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add($"{extension.TrimStart('.').ToUpper()} File", new[] { extension });
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.SuggestedFileName = fileName;

            var picked = await picker.PickSaveFileAsync();
            if (picked != null)
            {
                try
                {
                    var cached = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                    await cached.CopyAndReplaceAsync(picked);
                }
                catch { }
            }
        }

        public RelayCommand OpenWithCommand { get; }
        protected virtual async void OpenWithExecute()
        {
            var item = _selectedItem;
            if (item == null)
            {
                return;
            }

            var file = item.GetFile();
            if (file != null && file.Local.IsDownloadingCompleted)
            {
                try
                {
                    var temp = await StorageFile.GetFileFromPathAsync(file.Local.Path);

                    var options = new LauncherOptions();
                    options.DisplayApplicationPicker = true;

                    await Launcher.LaunchFileAsync(temp, options);

                }
                catch { }
            }
        }

        public void OpenMessage(GalleryItem galleryItem)
        {
            var message = galleryItem as GalleryMessageItem;
            if (message == null)
            {
                return;
            }

            ProtoService.Send(new OpenMessageContent(message.ChatId, message.Id));
        }
    }
}