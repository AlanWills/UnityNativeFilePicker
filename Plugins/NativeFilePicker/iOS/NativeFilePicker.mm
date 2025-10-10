#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

extern UIViewController* UnityGetGLViewController();

@interface UNativeFilePicker : NSObject
+ (void)pickFiles:(BOOL)allowMultipleSelection withUTIs:(NSArray<NSString *> *)allowedUTIs;
+ (void)exportFiles:(NSArray<NSURL *> *)paths;
+ (int)canPickMultipleFiles;
+ (int)isFilePickerBusy;
+ (char *)convertExtensionToUTI:(NSString *)extension;
@end

@implementation UNativeFilePicker

static UIDocumentPickerViewController *filePicker = nil;
static BOOL pickingMultipleFiles = NO;
static int filePickerState = 0; // 0 -> none, 1 -> showing, 2 -> finished

+ (void)pickFiles:(BOOL)allowMultipleSelection withUTIs:(NSArray<NSString *> *)allowedUTIs
{
    filePicker = [[UIDocumentPickerViewController alloc] initWithDocumentTypes:allowedUTIs
                                                                        inMode:UIDocumentPickerModeImport];
    filePicker.delegate = (id)self;
    filePicker.allowsMultipleSelection = allowMultipleSelection;
    filePicker.shouldShowFileExtensions = YES;

    pickingMultipleFiles = allowMultipleSelection;
    filePickerState = 1;

    [UnityGetGLViewController() presentViewController:filePicker animated:NO completion:^{
        filePickerState = 0;
    }];
}

+ (void)exportFiles:(NSArray<NSURL *> *)paths
{
    if (!paths || paths.count == 0)
        return;

    if (paths.count > 1 && [self canPickMultipleFiles] == 1)
        filePicker = [[UIDocumentPickerViewController alloc] initWithURLs:paths inMode:UIDocumentPickerModeExportToService];
    else
        filePicker = [[UIDocumentPickerViewController alloc] initWithURL:paths[0] inMode:UIDocumentPickerModeExportToService];

    filePicker.delegate = (id)self;
    filePicker.shouldShowFileExtensions = YES;

    filePickerState = 1;
    [UnityGetGLViewController() presentViewController:filePicker animated:NO completion:^{
        filePickerState = 0;
    }];
}

+ (int)canPickMultipleFiles
{
    return 1; // iOS 11+ always supports multiple selection
}

+ (int)isFilePickerBusy
{
    if (filePickerState == 2)
        return 1;

    if (filePicker != nil)
    {
        if (filePickerState == 1 || [filePicker presentingViewController] == UnityGetGLViewController())
            return 1;
        else
        {
            filePicker = nil;
            return 0;
        }
    }
    else
        return 0;
}

+ (char *)convertExtensionToUTI:(NSString *)extension
{
    // iOS 14+ only – use UniformTypeIdentifiers
    UTType *type = [UTType typeWithFilenameExtension:extension.lowercaseString];
    if (type != nil)
        return [self getCString:type.identifier];
    else
        return [self getCString:@"public.data"]; // fallback generic type
}

// Called when a single file was picked (iOS < 11 fallback not needed)
+ (void)documentPicker:(UIDocumentPickerViewController *)controller didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls
{
    [self documentPickerCompleted:controller documents:urls];
}

+ (void)documentPickerCompleted:(UIDocumentPickerViewController *)controller documents:(NSArray<NSURL *> *)urls
{
    filePicker = nil;
    filePickerState = 2;

    if (controller.documentPickerMode == UIDocumentPickerModeImport)
    {
        if (!pickingMultipleFiles || urls.count <= 1)
        {
            const char* filePath = urls.count > 0 ? [self getCString:urls[0].path] : "";
            if (pickingMultipleFiles)
                UnitySendMessage("FPResultCallbackiOS", "OnMultipleFilesPicked", filePath);
            else
                UnitySendMessage("FPResultCallbackiOS", "OnFilePicked", filePath);
        }
        else
        {
            NSMutableArray<NSString *> *filePaths = [NSMutableArray arrayWithCapacity:urls.count];
            for (NSURL *url in urls)
                [filePaths addObject:url.path];

            UnitySendMessage("FPResultCallbackiOS", "OnMultipleFilesPicked",
                             [self getCString:[filePaths componentsJoinedByString:@">"]]);
        }
    }
    else if (controller.documentPickerMode == UIDocumentPickerModeExportToService)
    {
        UnitySendMessage("FPResultCallbackiOS", "OnFilesExported", urls.count > 0 ? "1" : "0");
    }

    [controller dismissViewControllerAnimated:NO completion:nil];
}

+ (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller
{
    filePicker = nil;
    UnitySendMessage("FPResultCallbackiOS", "OnOperationCancelled", "");
    [controller dismissViewControllerAnimated:NO completion:nil];
}

+ (char *)getCString:(NSString *)source
{
    if (!source)
        source = @"";
    const char *utf8 = [source UTF8String];
    char *result = (char *)malloc(strlen(utf8) + 1);
    strcpy(result, utf8);
    return result;
}

@end

// -------- C interface (called from C#) --------

extern "C" void _NativeFilePicker_PickFile(const char* UTIs[], int UTIsCount)
{
    NSMutableArray<NSString *> *allowedUTIs = [NSMutableArray arrayWithCapacity:UTIsCount];
    for (int i = 0; i < UTIsCount; i++)
        [allowedUTIs addObject:[NSString stringWithUTF8String:UTIs[i]]];

    [UNativeFilePicker pickFiles:NO withUTIs:allowedUTIs];
}

extern "C" void _NativeFilePicker_PickMultipleFiles(const char* UTIs[], int UTIsCount)
{
    NSMutableArray<NSString *> *allowedUTIs = [NSMutableArray arrayWithCapacity:UTIsCount];
    for (int i = 0; i < UTIsCount; i++)
        [allowedUTIs addObject:[NSString stringWithUTF8String:UTIs[i]]];

    [UNativeFilePicker pickFiles:YES withUTIs:allowedUTIs];
}

extern "C" void _NativeFilePicker_ExportFiles(const char* files[], int filesCount)
{
    NSMutableArray<NSURL *> *paths = [NSMutableArray arrayWithCapacity:filesCount];
    for (int i = 0; i < filesCount; i++)
    {
        NSString *filePath = [NSString stringWithUTF8String:files[i]];
        [paths addObject:[NSURL fileURLWithPath:filePath]];
    }

    [UNativeFilePicker exportFiles:paths];
}

extern "C" int _NativeFilePicker_CanPickMultipleFiles()
{
    return [UNativeFilePicker canPickMultipleFiles];
}

extern "C" int _NativeFilePicker_IsFilePickerBusy()
{
    return [UNativeFilePicker isFilePickerBusy];
}

extern "C" char* _NativeFilePicker_ConvertExtensionToUTI(const char* extension)
{
    return [UNativeFilePicker convertExtensionToUTI:[NSString stringWithUTF8String:extension]];
}
