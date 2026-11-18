use windows::core::PCWSTR;
use windows::Win32::Storage::FileSystem::{
    DeleteFileW, ReplaceFileW, REPLACE_FILE_FLAGS,
};

/// Removes the Zone.Identifier NTFS Alternate Data Stream (Mark of the Web)
/// directly via Win32 DeleteFileW.
/// Returns 0 on success (or if ADS didn't exist), or Win32 error code.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn native_unblock_file(file_path: *const u16) -> i32 {
    if file_path.is_null() {
        return -1;
    }

    let mut len = 0;
    while unsafe { *file_path.add(len) } != 0 {
        len += 1;
    }
    let slice = unsafe { std::slice::from_raw_parts(file_path, len) };

    // Append ":Zone.Identifier\0"
    let stream_suffix: [u16; 17] = [
        ':' as u16, 'Z' as u16, 'o' as u16, 'n' as u16, 'e' as u16, '.' as u16,
        'I' as u16, 'd' as u16, 'e' as u16, 'n' as u16, 't' as u16, 'i' as u16,
        'f' as u16, 'i' as u16, 'e' as u16, 'r' as u16, 0,
    ];

    let mut full_path: Vec<u16> = Vec::with_capacity(slice.len() + stream_suffix.len());
    full_path.extend_from_slice(slice);
    full_path.extend_from_slice(&stream_suffix);

    let res = unsafe { DeleteFileW(PCWSTR(full_path.as_ptr())) };
    if let Err(e) = res {
        let code = e.code().0;
        // ERROR_FILE_NOT_FOUND (2) means stream was not present, which is totally normal
        if code == 2 || code == -2147024894 {
            return 0;
        }
        return code;
    }

    0
}

/// Atomically replaces destination_file with replaced_by_file using ReplaceFileW.
/// If backup_file is non-null, creates a backup of the destination file.
/// Returns 0 on success, or Win32 error code.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn native_replace_file(
    destination_file: *const u16,
    replaced_by_file: *const u16,
    backup_file: *const u16,
) -> i32 {
    if destination_file.is_null() || replaced_by_file.is_null() {
        return -1;
    }

    let backup_pcwstr = if backup_file.is_null() {
        PCWSTR::null()
    } else {
        PCWSTR(backup_file)
    };

    let flags = REPLACE_FILE_FLAGS(0); // Standard atomic replace

    let res = unsafe {
        ReplaceFileW(
            PCWSTR(destination_file),
            PCWSTR(replaced_by_file),
            backup_pcwstr,
            flags,
            None,
            None,
        )
    };

    match res {
        Ok(_) => 0,
        Err(e) => e.code().0,
    }
}
