// VideoWallpaper - простые видеообои для Windows 10/11.
// Только системные API: Win32 + Media Foundation (MFPlay). Внешних библиотек не нужно.

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <shellapi.h>
#include <commdlg.h>
#include <mfapi.h>
#include <mfplay.h>
#include <mferror.h>
#include <string>

namespace {

constexpr wchar_t kWndClass[] = L"VideoWallpaperWnd";
constexpr wchar_t kMutexName[] = L"VideoWallpaper_SingleInstance";
constexpr wchar_t kRunKey[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Run";
constexpr wchar_t kRunValue[] = L"VideoWallpaper";
constexpr UINT WM_TRAY = WM_APP + 1;
constexpr UINT kTrayId = 1;

enum : UINT {
    IDM_CHOOSE = 1001,
    IDM_PAUSE,
    IDM_AUTOSTART,
    IDM_EXIT,
};

HWND g_hwnd = nullptr;
IMFPMediaPlayer* g_player = nullptr;
std::wstring g_videoPath;
bool g_paused = false;

// ---------- Настройки (ini-файл рядом с exe) ----------

std::wstring IniPath() {
    wchar_t exe[MAX_PATH];
    GetModuleFileNameW(nullptr, exe, MAX_PATH);
    std::wstring p(exe);
    size_t slash = p.find_last_of(L"\\/");
    return p.substr(0, slash + 1) + L"VideoWallpaper.ini";
}

void LoadSettings() {
    wchar_t buf[MAX_PATH * 4];
    GetPrivateProfileStringW(L"Settings", L"Video", L"", buf, _countof(buf), IniPath().c_str());
    g_videoPath = buf;
}

void SaveSettings() {
    WritePrivateProfileStringW(L"Settings", L"Video", g_videoPath.c_str(), IniPath().c_str());
}

// ---------- Автозапуск (HKCU\...\Run) ----------

bool IsAutostart() {
    HKEY key;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, kRunKey, 0, KEY_READ, &key) != ERROR_SUCCESS)
        return false;
    LONG r = RegQueryValueExW(key, kRunValue, nullptr, nullptr, nullptr, nullptr);
    RegCloseKey(key);
    return r == ERROR_SUCCESS;
}

void SetAutostart(bool on) {
    HKEY key;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, kRunKey, 0, nullptr, 0, KEY_WRITE, nullptr, &key,
                        nullptr) != ERROR_SUCCESS)
        return;
    if (on) {
        wchar_t exe[MAX_PATH];
        GetModuleFileNameW(nullptr, exe, MAX_PATH);
        std::wstring value = L"\"";
        value += exe;
        value += L"\"";
        RegSetValueExW(key, kRunValue, 0, REG_SZ, reinterpret_cast<const BYTE*>(value.c_str()),
                       static_cast<DWORD>((value.size() + 1) * sizeof(wchar_t)));
    } else {
        RegDeleteValueW(key, kRunValue);
    }
    RegCloseKey(key);
}

// ---------- Проигрыватель (MFPlay) ----------

class PlayerCallback : public IMFPMediaPlayerCallback {
public:
    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) override {
        if (riid == IID_IUnknown || riid == __uuidof(IMFPMediaPlayerCallback)) {
            *ppv = static_cast<IMFPMediaPlayerCallback*>(this);
            AddRef();
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    STDMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&m_ref); }
    STDMETHODIMP_(ULONG) Release() override {
        ULONG r = InterlockedDecrement(&m_ref);
        if (r == 0) delete this;
        return r;
    }

    // Вызывается, когда видео доиграло до конца: перематываем на начало и играем снова.
    void STDMETHODCALLTYPE OnMediaPlayerEvent(MFP_EVENT_HEADER* header) override {
        if (!header) return;
        if (header->eEventType == MFP_EVENT_TYPE_PLAYBACK_ENDED && g_player) {
            g_player->Stop();
            if (!g_paused) g_player->Play();
        }
    }

private:
    LONG m_ref = 1;
};

void StopPlayer() {
    if (g_player) {
        g_player->Shutdown();
        g_player->Release();
        g_player = nullptr;
    }
}

bool StartPlayer() {
    StopPlayer();
    if (!g_hwnd || g_videoPath.empty()) return false;

    PlayerCallback* cb = new PlayerCallback();
    // autoPlay = TRUE, видео рисуется прямо в наше окно (внутри WorkerW).
    HRESULT hr = MFPCreateMediaPlayer(g_videoPath.c_str(), TRUE, 0, cb, g_hwnd, &g_player);
    cb->Release();
    if (FAILED(hr)) {
        g_player = nullptr;
        MessageBoxW(g_hwnd, L"Не удалось открыть видео.\nПопробуйте MP4 (H.264).",
                    L"VideoWallpaper", MB_ICONERROR);
        return false;
    }
    g_player->SetVolume(0.0f);  // обои без звука
    if (g_paused) g_player->Pause();
    return true;
}

// ---------- Размещение за иконками рабочего стола (трюк WorkerW) ----------

HWND FindWorkerW() {
    HWND progman = FindWindowW(L"Progman", nullptr);
    // Просим Explorer создать WorkerW-окно за иконками.
    DWORD_PTR result = 0;
    SendMessageTimeoutW(progman, 0x052C, 0, 0, SMTO_NORMAL, 1000, &result);

    HWND workerw = nullptr;
    EnumWindows(
        [](HWND top, LPARAM out) -> BOOL {
            // Окно с иконками (SHELLDLL_DefView) и следующее за ним WorkerW - это нужный слой.
            if (FindWindowExW(top, nullptr, L"SHELLDLL_DefView", nullptr)) {
                *reinterpret_cast<HWND*>(out) = FindWindowExW(nullptr, top, L"WorkerW", nullptr);
                return FALSE;
            }
            return TRUE;
        },
        reinterpret_cast<LPARAM>(&workerw));

    return workerw ? workerw : progman;
}

void PlaceOnDesktop() {
    HWND parent = FindWorkerW();
    SetWindowLongPtrW(g_hwnd, GWL_STYLE, WS_CHILD | WS_VISIBLE);
    SetParent(g_hwnd, parent);

    RECT rc;
    GetClientRect(parent, &rc);
    SetWindowPos(g_hwnd, HWND_BOTTOM, 0, 0, rc.right, rc.bottom,
                 SWP_NOACTIVATE | SWP_SHOWWINDOW);
}

// ---------- Трей и меню ----------

void AddTray() {
    NOTIFYICONDATAW nid{};
    nid.cbSize = sizeof(nid);
    nid.hWnd = g_hwnd;
    nid.uID = kTrayId;
    nid.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
    nid.uCallbackMessage = WM_TRAY;
    nid.hIcon = LoadIconW(nullptr, IDI_APPLICATION);
    wcscpy_s(nid.szTip, L"Видеообои");
    Shell_NotifyIconW(NIM_ADD, &nid);
}

void RemoveTray() {
    NOTIFYICONDATAW nid{};
    nid.cbSize = sizeof(nid);
    nid.hWnd = g_hwnd;
    nid.uID = kTrayId;
    Shell_NotifyIconW(NIM_DELETE, &nid);
}

void ChooseVideo() {
    wchar_t file[MAX_PATH] = {};
    OPENFILENAMEW ofn{};
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = g_hwnd;
    ofn.lpstrFilter = L"Видео\0*.mp4;*.wmv;*.avi;*.mov\0Все файлы\0*.*\0";
    ofn.lpstrFile = file;
    ofn.nMaxFile = MAX_PATH;
    ofn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST;
    if (GetOpenFileNameW(&ofn)) {
        g_videoPath = file;
        SaveSettings();
        StartPlayer();
    }
}

void ShowMenu() {
    HMENU menu = CreatePopupMenu();
    AppendMenuW(menu, MF_STRING, IDM_CHOOSE, L"Выбрать видео...");
    AppendMenuW(menu, MF_STRING, IDM_PAUSE, g_paused ? L"Продолжить" : L"Пауза");
    AppendMenuW(menu, MF_STRING | (IsAutostart() ? MF_CHECKED : 0), IDM_AUTOSTART,
                L"Автозапуск с Windows");
    AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(menu, MF_STRING, IDM_EXIT, L"Выход");

    POINT pt;
    GetCursorPos(&pt);
    SetForegroundWindow(g_hwnd);
    TrackPopupMenu(menu, TPM_RIGHTBUTTON, pt.x, pt.y, 0, g_hwnd, nullptr);
    PostMessageW(g_hwnd, WM_NULL, 0, 0);
    DestroyMenu(menu);
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp) {
    switch (msg) {
    case WM_TRAY:
        if (lp == WM_RBUTTONUP || lp == WM_CONTEXTMENU) ShowMenu();
        else if (lp == WM_LBUTTONDBLCLK) ChooseVideo();
        return 0;

    case WM_COMMAND:
        switch (LOWORD(wp)) {
        case IDM_CHOOSE:
            ChooseVideo();
            break;
        case IDM_PAUSE:
            g_paused = !g_paused;
            if (g_player) {
                if (g_paused) g_player->Pause();
                else g_player->Play();
            }
            break;
        case IDM_AUTOSTART:
            SetAutostart(!IsAutostart());
            break;
        case IDM_EXIT:
            DestroyWindow(hwnd);
            break;
        }
        return 0;

    case WM_SIZE:
        if (g_player) g_player->UpdateVideo();
        return 0;

    case WM_DISPLAYCHANGE:
        PlaceOnDesktop();
        return 0;

    case WM_DESTROY:
        StopPlayer();
        RemoveTray();
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

}  // namespace

int APIENTRY wWinMain(HINSTANCE hInst, HINSTANCE, LPWSTR, int) {
    // Запрещаем второй экземпляр.
    HANDLE mutex = CreateMutexW(nullptr, TRUE, kMutexName);
    if (GetLastError() == ERROR_ALREADY_EXISTS) return 0;

    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    MFStartup(MF_VERSION);

    WNDCLASSEXW wc{};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInst;
    wc.lpszClassName = kWndClass;
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    RegisterClassExW(&wc);

    g_hwnd = CreateWindowExW(0, kWndClass, L"VideoWallpaper", WS_POPUP, 0, 0, 1, 1, nullptr,
                             nullptr, hInst, nullptr);
    PlaceOnDesktop();
    AddTray();

    LoadSettings();
    if (g_videoPath.empty() || GetFileAttributesW(g_videoPath.c_str()) == INVALID_FILE_ATTRIBUTES)
        ChooseVideo();  // при первом запуске сразу просим выбрать видео
    else
        StartPlayer();

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0) > 0) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    MFShutdown();
    CoUninitialize();
    if (mutex) CloseHandle(mutex);
    return 0;
}
