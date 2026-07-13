use std::sync::Mutex;
use tauri::Manager;
use tauri_plugin_shell::ShellExt;

// Holds the API sidecar child process so it can be killed on app exit.
struct ApiSidecar(Mutex<Option<tauri_plugin_shell::process::CommandChild>>);

// Random per-launch token gating /api/filesystem/* on the sidecar.
struct FsToken(String);

#[tauri::command]
fn fs_token(state: tauri::State<FsToken>) -> String {
    state.0.clone()
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .manage(ApiSidecar(Mutex::new(None)))
        .manage(FsToken(uuid::Uuid::new_v4().to_string()))
        .invoke_handler(tauri::generate_handler![fs_token])
        .setup(|app| {
            if cfg!(debug_assertions) {
                app.handle().plugin(
                    tauri_plugin_log::Builder::default()
                        .level(log::LevelFilter::Info)
                        .build(),
                )?;
            }

            // Release builds only: start the bundled ASP.NET API sidecar.
            // In dev mode the API runs separately (npm run dev + dotnet watch).
            // The binary is at src-tauri/binaries/rAspCoreVueLauncher-api-{triple}.
            if !cfg!(debug_assertions) {
                let token = app.state::<FsToken>().0.clone();
                let (mut rx, child) = app
                    .handle()
                    .shell()
                    .sidecar("rAspCoreVueLauncher-api")?
                    .args(["--urls", "http://127.0.0.1:5202", "--fs-token", token.as_str()])
                    .spawn()?;
                *app.state::<ApiSidecar>().0.lock().unwrap() = Some(child);
                // Drain stdout/stderr so the pipe buffer never blocks the sidecar.
                tauri::async_runtime::spawn(async move {
                    use tauri_plugin_shell::process::CommandEvent;
                    while let Some(event) = rx.recv().await {
                        if let CommandEvent::Terminated(_) = event {
                            break;
                        }
                    }
                });
            }
            Ok(())
        })
        .on_window_event(|window, event| {
            if let tauri::WindowEvent::Destroyed = event {
                if let Some(state) = window.app_handle().try_state::<ApiSidecar>() {
                    if let Ok(mut guard) = state.0.lock() {
                        if let Some(child) = guard.take() {
                            let _ = child.kill();
                        }
                    }
                }
            }
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
