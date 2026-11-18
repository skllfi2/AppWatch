use std::fs::File;
use windows::core::{PCWSTR, GUID};
use windows::Win32::Foundation::{HANDLE, HWND};
use windows::Win32::Security::WinTrust::{
    WinVerifyTrust, WINTRUST_ACTION_GENERIC_VERIFY_V2, WINTRUST_DATA,
    WINTRUST_DATA_0, WINTRUST_DATA_PROVIDER_FLAGS, WINTRUST_DATA_UICONTEXT,
    WINTRUST_FILE_INFO, WTD_CHOICE_FILE, WTD_REVOKE_NONE,
    WTD_STATEACTION_CLOSE, WTD_STATEACTION_VERIFY, WTD_UI_NONE,
};

#[repr(C)]
#[derive(Debug, Default, Clone, Copy)]
pub struct NativePeInfo {
    pub is_64bit: bool,
    pub is_arm64: bool,
    pub is_signed: bool,
    pub is_trusted: bool,
    pub file_size: u64,
}

const IMAGE_FILE_MACHINE_AMD64: u16 = 0x8664;
const IMAGE_FILE_MACHINE_ARM64: u16 = 0xaa64;

/// Inspects PE file headers and signature status via zero-copy memory mapping
/// and WinVerifyTrust.
pub fn inspect_pe_internal(path_w: &[u16]) -> Result<NativePeInfo, i32> {
    use std::os::windows::ffi::OsStringExt;
    let os_str = std::ffi::OsString::from_wide(path_w);
    let path = std::path::Path::new(&os_str);

    let file = File::open(path).map_err(|_| -2)?;
    let metadata = file.metadata().map_err(|_| -3)?;
    let file_size = metadata.len();

    let mmap = unsafe { memmap2::Mmap::map(&file).map_err(|_| -4)? };

    if mmap.len() < 64 {
        return Err(-5); // Not a valid PE
    }

    // Check MZ signature
    if mmap[0] != b'M' || mmap[1] != b'Z' {
        return Err(-6);
    }

    let e_lfanew = u32::from_le_bytes([mmap[60], mmap[61], mmap[62], mmap[63]]) as usize;
    if e_lfanew + 24 > mmap.len() {
        return Err(-7);
    }

    // Check PE\0\0 signature
    if &mmap[e_lfanew..e_lfanew + 4] != b"PE\0\0" {
        return Err(-8);
    }

    let machine = u16::from_le_bytes([mmap[e_lfanew + 4], mmap[e_lfanew + 5]]);
    let is_64bit = machine == IMAGE_FILE_MACHINE_AMD64;
    let is_arm64 = machine == IMAGE_FILE_MACHINE_ARM64;

    // Check signature via WinVerifyTrust
    let (is_signed, is_trusted) = verify_authenticode(path_w);

    Ok(NativePeInfo {
        is_64bit,
        is_arm64,
        is_signed,
        is_trusted,
        file_size,
    })
}

fn verify_authenticode(path_w: &[u16]) -> (bool, bool) {
    let mut null_terminated: Vec<u16> = path_w.to_vec();
    if null_terminated.is_empty() || *null_terminated.last().unwrap() != 0 {
        null_terminated.push(0);
    }

    let mut file_info = WINTRUST_FILE_INFO {
        cbStruct: std::mem::size_of::<WINTRUST_FILE_INFO>() as u32,
        pcwszFilePath: PCWSTR(null_terminated.as_ptr()),
        hFile: HANDLE::default(),
        pgKnownSubject: std::ptr::null_mut(),
    };

    let mut action_id: GUID = WINTRUST_ACTION_GENERIC_VERIFY_V2;

    let mut trust_data = WINTRUST_DATA {
        cbStruct: std::mem::size_of::<WINTRUST_DATA>() as u32,
        pPolicyCallbackData: std::ptr::null_mut(),
        pSIPClientData: std::ptr::null_mut(),
        dwUIChoice: WTD_UI_NONE,
        fdwRevocationChecks: WTD_REVOKE_NONE,
        dwUnionChoice: WTD_CHOICE_FILE,
        Anonymous: WINTRUST_DATA_0 {
            pFile: &mut file_info,
        },
        dwStateAction: WTD_STATEACTION_VERIFY,
        hWVTStateData: HANDLE::default(),
        pwszURLReference: windows::core::PWSTR::null(),
        dwProvFlags: WINTRUST_DATA_PROVIDER_FLAGS(0x00000080), // WTD_CACHE_ONLY_URL_RETRIEVAL
        dwUIContext: WINTRUST_DATA_UICONTEXT(0),
        pSignatureSettings: std::ptr::null_mut(),
    };

    let status = unsafe {
        WinVerifyTrust(
            HWND::default(),
            &mut action_id,
            &mut trust_data as *mut _ as *mut std::ffi::c_void,
        )
    };

    // Close trust state
    trust_data.dwStateAction = WTD_STATEACTION_CLOSE;
    unsafe {
        let _ = WinVerifyTrust(
            HWND::default(),
            &mut action_id,
            &mut trust_data as *mut _ as *mut std::ffi::c_void,
        );
    }

    // 0 = ERROR_SUCCESS (signed and trusted)
    // TRUST_E_NOSIGNATURE = 0x800B0100 (-2146762496)
    let is_trusted = status == 0;
    let is_signed = status != -2146762496i32;

    (is_signed, is_trusted)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn native_inspect_pe(
    file_path: *const u16,
    out_info: *mut NativePeInfo,
) -> i32 {
    if file_path.is_null() || out_info.is_null() {
        return -1;
    }

    // Read null-terminated wide string
    let mut len = 0;
    while unsafe { *file_path.add(len) } != 0 {
        len += 1;
    }
    let slice = unsafe { std::slice::from_raw_parts(file_path, len) };

    match inspect_pe_internal(slice) {
        Ok(info) => {
            unsafe { *out_info = info; }
            0
        }
        Err(code) => code,
    }
}
