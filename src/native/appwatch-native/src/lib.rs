pub mod restart_manager;
pub mod pe_analyzer;
pub mod file_ops;

pub use restart_manager::*;
pub use pe_analyzer::*;
pub use file_ops::*;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_inspect_current_exe() {
        let exe_path = std::env::current_exe().expect("failed to get current_exe");
        let wide: Vec<u16> = exe_path.to_str().unwrap().encode_utf16().chain(std::iter::once(0)).collect();
        let mut info = NativePeInfo::default();
        let res = unsafe { native_inspect_pe(wide.as_ptr(), &mut info) };
        assert_eq!(res, 0);
        assert!(info.file_size > 0);
        assert!(info.is_64bit);
    }
}
