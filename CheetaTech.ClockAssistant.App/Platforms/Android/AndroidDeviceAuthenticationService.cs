using Android.OS;
using AndroidX.Biometric;
using AndroidX.Fragment.App;
using Java.Lang;
using Microsoft.Maui.ApplicationModel;

namespace CheetaTech.ClockAssistant.App.Services.Security;

public sealed class AndroidDeviceAuthenticationService
    : IDeviceAuthenticationService
{
    public async Task<DeviceAuthenticationStatus> AuthenticateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var activity =
            Platform.CurrentActivity
            as FragmentActivity;

        if (activity is null)
        {
            return DeviceAuthenticationStatus.Unavailable;
        }

        var allowedAuthenticators =
            Build.VERSION.SdkInt >= BuildVersionCodes.R
                ? BiometricManager.Authenticators.BiometricStrong
                    | BiometricManager.Authenticators.DeviceCredential
                : BiometricManager.Authenticators.BiometricWeak
                    | BiometricManager.Authenticators.DeviceCredential;

        var biometricManager =
            BiometricManager.From(activity);

        var availability =
            biometricManager.CanAuthenticate(
                allowedAuthenticators);

        if (availability
            == BiometricManager.BiometricErrorNoneEnrolled)
        {
            return DeviceAuthenticationStatus.NotConfigured;
        }

        if (availability
            != BiometricManager.BiometricSuccess)
        {
            return DeviceAuthenticationStatus.Unavailable;
        }

        var completion =
            new TaskCompletionSource<DeviceAuthenticationStatus>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var callback =
            new AuthenticationCallback(completion);

        var prompt =
            new BiometricPrompt(
                activity,
                callback);

        var promptInfo =
            new BiometricPrompt.PromptInfo.Builder()
                .SetTitle("Unlock Settings")
                .SetSubtitle(
                    "Verify your identity to access Clock Assistant settings.")
                .SetAllowedAuthenticators(allowedAuthenticators)
                .Build();

        using var cancellationRegistration =
            cancellationToken.Register(
                () =>
                {
                    prompt.CancelAuthentication();

                    completion.TrySetResult(
                        DeviceAuthenticationStatus.CanceledOrFailed);
                });

        activity.RunOnUiThread(
            () => prompt.Authenticate(promptInfo));

        return await completion.Task.ConfigureAwait(false);
    }

    private sealed class AuthenticationCallback
        : BiometricPrompt.AuthenticationCallback
    {
        private readonly
            TaskCompletionSource<DeviceAuthenticationStatus> _completion;

        public AuthenticationCallback(
            TaskCompletionSource<DeviceAuthenticationStatus> completion)
        {
            _completion =
                completion
                ?? throw new ArgumentNullException(nameof(completion));
        }

        public override void OnAuthenticationSucceeded(
            BiometricPrompt.AuthenticationResult result)
        {
            _completion.TrySetResult(
                DeviceAuthenticationStatus.Succeeded);
        }

        public override void OnAuthenticationError(
            int errorCode,
            ICharSequence errString)
        {
            // Do not surface or log platform authentication details here.
            _completion.TrySetResult(
                DeviceAuthenticationStatus.CanceledOrFailed);
        }

        public override void OnAuthenticationFailed()
        {
            // A rejected biometric attempt is not final; the AndroidX
            // prompt remains active and allows the user to try again.
        }
    }
}