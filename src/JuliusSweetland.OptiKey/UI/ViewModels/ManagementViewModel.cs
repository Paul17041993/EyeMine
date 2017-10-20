using JuliusSweetland.OptiKey.Properties;
using JuliusSweetland.OptiKey.Services;
using JuliusSweetland.OptiKey.UI.ViewModels.Management;
using log4net;
using Prism.Commands;
using Prism.Interactivity.InteractionRequest;
using Prism.Mvvm;
using System.Windows;

namespace JuliusSweetland.OptiKey.UI.ViewModels
{
    public class ManagementViewModel : BindableBase
    {
        #region Private Member Vars

        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #endregion
        
        #region Ctor

        public ManagementViewModel(
            IAudioService audioService,
            IDictionaryService dictionaryService)
        {
            //Instantiate child VMs
            DictionaryViewModel = new DictionaryViewModel(dictionaryService);
            OtherViewModel = new OtherViewModel();
            PointingAndSelectingViewModel = new PointingAndSelectingViewModel();
            VisualsViewModel = new VisualsViewModel();
            
            //Instantiate interaction requests and commands
            ConfirmationRequest = new InteractionRequest<Confirmation>();
            OkCommand = new DelegateCommand<Window>(Ok); //Can always click Ok
            CancelCommand = new DelegateCommand<Window>(Cancel); //Can always click Cancel
        }
        
        #endregion
        
        #region Properties
        
        public bool ChangesRequireRestart
        {
            get
            {
                return DictionaryViewModel.ChangesRequireRestart
                    || OtherViewModel.ChangesRequireRestart
                    || PointingAndSelectingViewModel.ChangesRequireRestart
                    || VisualsViewModel.ChangesRequireRestart;
            }
        }

        public DictionaryViewModel DictionaryViewModel { get; private set; }
        public OtherViewModel OtherViewModel { get; private set; }
        public PointingAndSelectingViewModel PointingAndSelectingViewModel { get; private set; }
        public VisualsViewModel VisualsViewModel { get; private set; }
        
        public InteractionRequest<Confirmation> ConfirmationRequest { get; private set; }
        public DelegateCommand<Window> OkCommand { get; private set; }
        public DelegateCommand<Window> CancelCommand { get; private set; }
        
        #endregion
        
        #region Methods

        private void ApplyChanges()
        {
            DictionaryViewModel.ApplyChanges();
            OtherViewModel.ApplyChanges();
            PointingAndSelectingViewModel.ApplyChanges();
            VisualsViewModel.ApplyChanges();
        }

        private void Ok(Window window)
        {
            if (ChangesRequireRestart)
            {
                //Warn if restart required and prompt for Confirmation before restarting
                ConfirmationRequest.Raise(
                    new Confirmation
                    {
                        Title = Resources.VERIFY_RESTART,
                        Content = Resources.RESTART_MESSAGE
                    }, confirmation =>
                    {
                        if (confirmation.Confirmed)
                        {
                            Log.Info("Applying management changes and attempting to restart OptiKey");
                            ApplyChanges();
                            try
                            {
                                System.Windows.Forms.Application.Restart();
                            }
                            catch { } //Swallow any exceptions (e.g. DispatcherExceptions) - we're shutting down so it doesn't matter.
                            Application.Current.Shutdown();
                        }
                    });
            }
            else
            {
                Log.Info("Applying management changes");
                ApplyChanges();
                window.Close();
            }
        }

        private static void Cancel(Window window)
        {
            window.Close();
        }

        #endregion
    }
}
