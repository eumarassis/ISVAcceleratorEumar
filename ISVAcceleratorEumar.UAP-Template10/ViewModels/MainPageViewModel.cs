using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Sync;
using Sample.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Template10.Services.NavigationService;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.WindowsAzure.MobileServices.SQLiteStore;
using Sample.Services.SettingsServices;
using System.Collections.ObjectModel;

namespace Sample.ViewModels
{
    public class MainPageViewModel : Sample.Mvvm.ViewModelBase
    {
        private MobileServiceCollection<TodoItem, TodoItem> _itemsMobileService;
        private IMobileServiceSyncTable<TodoItem> _todoTable ; // offline sync

        private IMobileServiceClient _mobileService;
     
        //private IMobileServiceTable<TodoItem> todoTable = App.MobileService.GetTable<TodoItem>();

        public MainPageViewModel()
        {
            _mobileService  = new MobileServiceClient(
            "https://isvacceleratoreumar.azurewebsites.net",
            "https://default-web-eastus744386286bdd4ad5816e8100d15de451.azurewebsites.net",
            "");

            _todoTable = _mobileService.GetSyncTable<TodoItem>();
        }

        #region Navigation
        public async override void OnNavigatedTo(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (state.Any())
            {
                // use cache value(s)
                if (state.ContainsKey(nameof(Value))) Value = state[nameof(Value)]?.ToString();
                // clear any cache
                state.Clear();
            }

            await InitLocalStoreAsync(); // offline sync
            await RefreshTodoItems();
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> state, bool suspending)
        {
            if (suspending)
            {
                // persist into cache
                state[nameof(Value)] = Value;
            }


            return base.OnNavigatedFromAsync(state, suspending);
        }

        public override void OnNavigatingFrom(NavigatingEventArgs args)
        {
            base.OnNavigatingFrom(args);
        }
        

        public void GotoDetailsPage()
        {
            this.NavigationService.Navigate(typeof(Views.DetailPage), this.Value);
        }

        #endregion

        #region Properties
        private string _Value = string.Empty;
        public string Value { get { return _Value; } set { Set(ref _Value, value); } }

        ObservableCollection<TodoItem> _ItemsSource = default(ObservableCollection<TodoItem>);

        public ObservableCollection<TodoItem> ItemsSource { get { return _ItemsSource; } set { Set(ref _ItemsSource, value); } }

        private string _NewTodoText = string.Empty;
        public string NewTodoText
        {
            get { return _NewTodoText; }
            set {
                Set(ref _NewTodoText, value);
            }
        }


        private bool _SaveIsEnabled = true;
        public bool SaveIsEnabled { get { return _SaveIsEnabled; } set { Set(ref _SaveIsEnabled, value); } }


        private bool _RefreshIsEnabled = true;
        public bool RefreshIsEnabled { get { return _RefreshIsEnabled; } set { Set(ref _RefreshIsEnabled, value); } }

        #endregion

        #region Buttons
        internal async void ButtonRefreshClick(object sender, RoutedEventArgs e)
        {
            RefreshIsEnabled = false;

            //await SyncAsync(); // offline sync
            await RefreshTodoItems();

            RefreshIsEnabled = true;
        }

        internal async void ButtonSaveClick(object sender, RoutedEventArgs e)
        {
            var todoItem = new TodoItem { Text = NewTodoText };
            await InsertTodoItem(todoItem);
        }

        internal async void CheckBoxCompleteChecked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            TodoItem item = cb.DataContext as TodoItem;
            await UpdateCheckedTodoItem(item);
        }


        #endregion

        #region App Login

        public async Task RefreshTodoItems()
        {
            MobileServiceInvalidOperationException exception = null;
            try
            {
                // This code refreshes the entries in the list view by querying the TodoItems table.
                // The query excludes completed TodoItems
                _itemsMobileService = await _todoTable
                    .Where(todoItem => todoItem.Complete == false)
                    .ToCollectionAsync();
            }
            catch (MobileServiceInvalidOperationException e)
            {
                exception = e;
            }

            if (exception != null)
            {
                await new MessageDialog(exception.Message, "Error loading items").ShowAsync();
            }
            else
            {
                RefreshItensSource();
                this.SaveIsEnabled = true;
            }
        }

        private void RefreshItensSource()
        {
            ItemsSource = new ObservableCollection<TodoItem>(_itemsMobileService);
        }

        private async Task UpdateCheckedTodoItem(TodoItem item)
        {
            // This code takes a freshly completed TodoItem and updates the database. When the MobileService 
            // responds, the item is removed from the list 
            await _todoTable.UpdateAsync(item);
            _itemsMobileService.Remove(item);
            //ListItems.Focus(Windows.UI.Xaml.FocusState.Unfocused);
            RefreshItensSource();
            await SyncAsync(); // offline sync
        }

        private async Task InsertTodoItem(TodoItem todoItem)
        {
            // This code inserts a new TodoItem into the database. When the operation completes
            // and Mobile Services has assigned an Id, the item is added to the CollectionView
            await _todoTable.InsertAsync(todoItem);
            _itemsMobileService.Add(todoItem);
            RefreshItensSource();
            await SyncAsync(); // offline sync
        }

        private async Task InitLocalStoreAsync()
        {
            if (!_mobileService.SyncContext.IsInitialized)
            {
                var store = new MobileServiceSQLiteStore("localstore.db");
                store.DefineTable<TodoItem>();
                await _mobileService.SyncContext.InitializeAsync(store);
            }

            await SyncAsync();
        }

        private async Task SyncAsync()
        {

            string message = null;
            try
            {
                //Simple
                await _mobileService.SyncContext.PushAsync();

                await _todoTable.PullAsync("todoItems", _todoTable.CreateQuery());
            }
            catch (MobileServicePushFailedException ex)
            {
                message = "Erro ao sincronizar - Qtd:" + ex.PushResult.Errors.Count + "- Mensagem: " + ex.Message;

            }
            catch (Exception ex)
            {
                message = "Erro generico ao sincronizar - Mensagem: " + ex.Message;
            }

            if (message != null)
            {
               MessageDialog m = new MessageDialog(message);

                await m.ShowAsync();
            }
        }


        #endregion
    }
}
