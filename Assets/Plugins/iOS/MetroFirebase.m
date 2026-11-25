// The iOS half of the game's Firebase Analytics bridge (Scripts/Game/Monetization/FirebaseAnalyticsService.cs).
// Three C functions around the native SDK; see Docs/ANALYTICS.md.
//
// The SDK comes from the FirebaseAnalytics pod (Assets/MetroSort/Editor/MetroFirebaseDependencies.xml). If the
// pod is ever removed this file still compiles and reports "not configured", so the game simply sends nothing.

#import <Foundation/Foundation.h>

#if __has_include(<FirebaseCore/FirebaseCore.h>) && __has_include(<FirebaseAnalytics/FirebaseAnalytics.h>)
#import <FirebaseCore/FirebaseCore.h>
#import <FirebaseAnalytics/FirebaseAnalytics.h>
#define METRO_HAS_FIREBASE 1
#else
#define METRO_HAS_FIREBASE 0
#endif

// Starts Firebase with the GoogleService-Info.plist the build copied into the app (FirebaseBuildSteps.cs).
// Returns 1 when Firebase is running, 0 when this build has no configuration.
int MetroFirebase_Configure(void)
{
#if METRO_HAS_FIREBASE
    // Asking Firebase for its app before it is configured logs an error, so the game keeps its own flag.
    static BOOL configured = NO;
    if (configured) return 1;
    // Without its file Firebase would stop the app, so the file is checked first. (No @try here: Unity builds
    // its Xcode project with Objective-C exceptions switched off.)
    NSString *path = [[NSBundle mainBundle] pathForResource:@"GoogleService-Info" ofType:@"plist"];
    if (path == nil) return 0;
    NSDictionary *config = [NSDictionary dictionaryWithContentsOfFile:path];
    if (![config[@"GOOGLE_APP_ID"] isKindOfClass:[NSString class]]) return 0;
    [FIRApp configure];
    configured = [FIRApp defaultApp] != nil;
    return configured ? 1 : 0;
#else
    return 0;
#endif
}

// Collection is off by default (FIREBASE_ANALYTICS_COLLECTION_ENABLED = NO in Info.plist); the game switches it
// on once consent allows it. Firebase remembers the choice between sessions.
void MetroFirebase_SetEnabled(int enabled)
{
#if METRO_HAS_FIREBASE
    [FIRAnalytics setAnalyticsCollectionEnabled:(enabled != 0)];
#endif
}

// One event; the parameters arrive as a flat JSON object of strings and numbers.
void MetroFirebase_LogEvent(const char *name, const char *json)
{
#if METRO_HAS_FIREBASE
    if (name == NULL) return;
    NSString *eventName = [NSString stringWithUTF8String:name];
    if (eventName == nil) return;
    NSDictionary *parameters = nil;
    if (json != NULL)
    {
        NSData *data = [NSData dataWithBytes:json length:strlen(json)];
        id parsed = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
        if ([parsed isKindOfClass:[NSDictionary class]] && [(NSDictionary *)parsed count] > 0) parameters = parsed;
    }
    [FIRAnalytics logEventWithName:eventName parameters:parameters];
#endif
}
