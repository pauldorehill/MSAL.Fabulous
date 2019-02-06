# Fabulous MSAL (Microsoft Authentication Library)
Example Fabulous app that uses [MSAL](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet) to authenticate an user on Azure Active Directory.

Feedback is welcome.

When I first started trying to use MSAL it was working great for WPF (not sure why), but failing on Android/UWP: from investigating more it was down to blocking on the UI thread by using `Async.RunSynchronously`. Typically when waiting for a task to return I would write something like:
```
let authResult =
    AcquireTokenAsync(...)
    |> Async.AwaitTask
    |> Async.RunSynchronously
```
However the issue with this is the UI thread can't be blocked to allow MSAL to open the required login windows. Thanks to this excellent article by [Tomas Petricek](http://tomasp.net/blog/async-non-blocking-gui.aspx/) I could solve the problem by using `Async.StartChild` in combination with `Cmd.ofAsyncMsg`.

This was a good learning experience as the MSAL examples are all in C# using `await`, but `await` behaves differently to `Async.RunSynchronously` in that it is [non blocking: ](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/await)'...An await expression does not block the thread on which it is executing...'

### FSharp.Azure.MSAL

This project wraps the MSAL library and returns a DU of `SignInStatus` which you can then call the two members `SignInStatus.SignIn scopes` or `SignInStatus.SignOut ()` to authenticate a user. To create an instance of `SignInStatus` use `SignInStatus.create ...` which requires you to tell it what `ApplicationPlatform` you are using.

Despite only been a single file this is a separate project as I plan to use it in other projects I am working on.

### iOS
I don't have access to a Mac so I haven't been able to complete the iOS project - I've gone as far as possible following the [guide.](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/Xamarin-ios-specificities)
Please feel free to finish it off.

### UWP

The UWP app is C# - I don't believe there is another way?

### WPF

This project is set to an output type of `Console Application` (rather than `Windows Application`) so that a console window appears for all the logs.

For WPF if you want to persist login info you need to implement a local storage cache. The DU case `ApplicationPlatfrom.WPF` requires you to choose the WPF Caching option:
```
| Default // Will create and use a cache in a .msalcache.bin file in the executing assembly folder
| NoCache // No caching
| CustomLocation of FileInfo // Pass in a custom file where to store the cache
| Custom of TokenCache // Pass your own cache e.g. if you were to use a database etc.
```
### Exceptions

I'm new to Xamarin.Forms but it seems that it's not possible to catch non UI thread exceptions in the shared project, but rather it has to be implemented on the platform specific projects. MSAL will throw outside of the UI thread if the login window is closed/cancelled etc.