use windows::core::PCWSTR;
use windows::Win32::Foundation::{ERROR_MORE_DATA, ERROR_SUCCESS};
use windows::Win32::System::RestartManager::{
    RmEndSession, RmGetList, RmRegisterResources, RmStartSession, CCH_RM_SESSION_KEY,
    RM_PROCESS_INFO,
};

/// RAII wrapper around Restart Manager session handle.
/// Guarantees RmEndSession is called when the session falls out of scope,
/// completely preventing handle leaks.
pub struct RmSession(u32);

impl RmSession {
    pub fn start() -> Result<Self, u32> {
        let mut session_handle = 0u32;
        let mut session_key = [0u16; CCH_RM_SESSION_KEY as usize + 1];
        unsafe {
            let err = RmStartSession(&mut session_handle, 0, windows::core::PWSTR(session_key.as_mut_ptr()));
            if err != ERROR_SUCCESS {
                return Err(err.0);
            }
        }
        Ok(Self(session_handle))
    }

    pub fn handle(&self) -> u32 {
        self.0
    }
}

impl Drop for RmSession {
    fn drop(&mut self) {
        if self.0 != 0 {
            unsafe {
                let _ = RmEndSession(self.0);
            }
        }
    }
}

/// Discovers PIDs of processes holding file locks on `file_path`.
/// Returns 0 on success (or no lockers found), or Win32 error code on failure.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn native_get_locking_processes(
    file_path: *const u16,
    out_pids: *mut u32,
    max_pids: u32,
    out_count: *mut u32,
) -> i32 {
    if file_path.is_null() || out_pids.is_null() || out_count.is_null() || max_pids == 0 {
        return -1;
    }

    unsafe { *out_count = 0; }

    let session = match RmSession::start() {
        Ok(s) => s,
        Err(e) => return e as i32,
    };

    let files = [PCWSTR(file_path)];
    let reg_res = unsafe {
        RmRegisterResources(
            session.handle(),
            Some(&files),
            None,
            None,
        )
    };
    if reg_res != ERROR_SUCCESS {
        return reg_res.0 as i32;
    }

    let mut n_proc_info_needed = 0u32;
    let mut n_proc_info = 0u32;
    let mut reboot_reasons = 0u32;

    // First call to get required buffer size
    let mut res = unsafe {
        RmGetList(
            session.handle(),
            &mut n_proc_info_needed,
            &mut n_proc_info,
            None,
            &mut reboot_reasons,
        )
    };

    if res != ERROR_SUCCESS && res != ERROR_MORE_DATA {
        return res.0 as i32;
    }

    if n_proc_info_needed == 0 {
        return 0;
    }

    let mut proc_info: Vec<RM_PROCESS_INFO> = vec![RM_PROCESS_INFO::default(); n_proc_info_needed as usize];
    n_proc_info = n_proc_info_needed;

    res = unsafe {
        RmGetList(
            session.handle(),
            &mut n_proc_info_needed,
            &mut n_proc_info,
            Some(proc_info.as_mut_ptr()),
            &mut reboot_reasons,
        )
    };

    if res != ERROR_SUCCESS {
        return res.0 as i32;
    }

    let count_to_copy = (n_proc_info as usize).min(max_pids as usize);
    for i in 0..count_to_copy {
        unsafe {
            *out_pids.add(i) = proc_info[i].Process.dwProcessId;
        }
    }

    unsafe {
        *out_count = count_to_copy as u32;
    }

    0
}
