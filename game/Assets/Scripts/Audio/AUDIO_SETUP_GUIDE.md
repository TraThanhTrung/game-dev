# HƯỚNG DẪN SETUP AUDIO SYSTEM

Hướng dẫn này sẽ giúp bạn setup audio system cho game Unity.

---

## BƯỚC 1: CHUẨN BỊ FILE ÂM THANH

1. **Import file MP3 vào Unity:**

   - Tạo thư mục `Assets/Audio/` (hoặc `Assets/Resources/Audio/`)
   - Kéo thả file MP3 vào thư mục này
   - Unity sẽ tự động import file

2. **Các file audio cần chuẩn bị (tối thiểu):**
   - **BGM (Background Music):** 1 file MP3 cho nhạc nền (nên là file dài, sẽ loop)
   - **CombatHit:** 1 file MP3 cho âm thanh khi tấn công
   - **UIClick:** 1 file MP3 cho âm thanh khi click UI
   - **ShopOpen:** 1 file MP3 cho âm thanh khi mở shop (optional)

---

## BƯỚC 2: TẠO AUDIO CONFIG SCRIPTABLEOBJECT

### Giải thích về Project Window:

- **Project Window** là cửa sổ ở dưới cùng (hoặc bên trái) trong Unity Editor
- Đây là nơi hiển thị tất cả các folder và file trong thư mục `Assets/` của project
- Bạn sẽ thấy các folder như: `Scripts/`, `Prefabs/`, `Scenes/`, `Sprites/`, v.v.
- **Đây chính là phần "Project" bao gồm các folder của bạn!**

### Các bước chi tiết:

1. **Tìm Project Window:**

   - Mở Unity Editor
   - Tìm cửa sổ **Project** (thường ở dưới cùng hoặc bên trái màn hình)
   - Nếu không thấy, vào menu `Window > General > Project` để mở

2. **Tạo AudioConfig asset:**

   - Trong **Project window**, điều hướng đến thư mục bạn muốn (ví dụ: `Assets/Scripts/Audio/` hoặc `Assets/Resources/`)
   - **Click chuột phải** vào vùng trống trong Project window (hoặc click chuột phải vào folder)
   - Một menu sẽ hiện ra, chọn: **`Create`** → **`Game`** → **`Audio Config`**
   - Một file mới sẽ được tạo với tên `AudioConfig.asset`
   - **Đổi tên** (nếu cần): Click vào file, nhấn F2 hoặc click chuột phải → Rename
   - Đặt tên: `AudioConfig` (hoặc tên bạn muốn, ví dụ: `GameAudioConfig`)

3. **Assign audio clips vào AudioConfig:**

   - **Chọn** file `AudioConfig.asset` vừa tạo (click vào nó trong Project window)
   - Ở bên phải, cửa sổ **Inspector** sẽ hiển thị các thuộc tính của AudioConfig
   - Bạn sẽ thấy các field như:

     - **BGM Clip** (Background Music)
     - **Combat Hit SFX** (Sound Effect)
     - **UI Click SFX** (Sound Effect)
     - **Shop Open SFX** (Sound Effect)

   - **Giải thích từng loại âm thanh:**

     **1. BGM Clip (Background Music - Nhạc nền):**

     - **Mục đích:** Nhạc nền chạy liên tục trong suốt game
     - **Đặc điểm:** File dài (thường 1-3 phút), sẽ tự động loop (lặp lại)
     - **Khi nào phát:** Tự động phát khi game bắt đầu và chạy liên tục
     - **Ví dụ:** Nhạc orchestral, ambient, hoặc theme music
     - **Gợi ý:** Nên dùng file MP3 chất lượng tốt, có thể loop mượt mà

     **2. Combat Hit SFX (Âm thanh tấn công):**

     - **Mục đích:** Âm thanh khi player tấn công enemy bằng vũ khí cận chiến (sword)
     - **Khi nào phát:** Khi player nhấn nút "Slash" và animation tấn công chạy
     - **Đặc điểm:** File ngắn (0.5-2 giây), sắc nét, có impact
     - **Ví dụ:** Tiếng "swish", "clang", "hit", hoặc "slash"
     - **Gợi ý:** Nên là âm thanh rõ ràng, không quá dài

     **3. UI Click SFX (Âm thanh click UI):**

     - **Mục đích:** Âm thanh khi player tương tác với UI elements
     - **Khi nào phát:**
       - Khi click vào inventory slot
       - Khi click vào shop button (mua item)
       - Khi click vào các nút trong shop (chuyển tab Items/Weapons/Armour)
     - **Đặc điểm:** File rất ngắn (0.1-0.5 giây), nhẹ nhàng, không gây khó chịu
     - **Ví dụ:** Tiếng "click", "beep", "pop", hoặc "tap"
     - **Gợi ý:** Nên là âm thanh nhẹ, không quá to, vì sẽ phát nhiều lần

     **4. Shop Open SFX (Âm thanh mở shop):**

     - **Mục đích:** Âm thanh khi player mở shop interface
     - **Khi nào phát:** Khi player nhấn nút "Interact" khi đứng gần shopkeeper và shop mở ra
     - **Đặc điểm:** File ngắn (0.5-2 giây), có thể là âm thanh "mở cửa" hoặc "notification"
     - **Ví dụ:** Tiếng "ding", "chime", "door open", hoặc "shop bell"
     - **Gợi ý:** Có thể bỏ qua nếu không có (không bắt buộc)

   - **Kéo thả các file MP3 từ Project window vào các field tương ứng:**
     - Tìm file BGM MP3 trong Project window → **Kéo** vào field **BGM Clip**
     - Tìm file combat hit MP3 → **Kéo** vào field **Combat Hit SFX**
     - Tìm file UI click MP3 → **Kéo** vào field **UI Click SFX**
     - Tìm file shop open MP3 → **Kéo** vào field **Shop Open SFX** (nếu có, không bắt buộc)
   - Sau khi kéo thả xong, các field sẽ hiển thị tên file audio

### Lưu ý:

- Nếu không thấy menu `Create > Game > Audio Config`, đợi Unity compile scripts xong (xem góc dưới bên phải)
- Nếu vẫn không thấy, kiểm tra Console (Window > General > Console) xem có lỗi compile không

---

## BƯỚC 3: SETUP AUDIOMANAGER TRONG SCENE

1. **Tạo AudioManager GameObject:**

   - Trong scene chính (thường là scene có GameManager)
   - Click chuột phải vào **Hierarchy** (ở root level, KHÔNG cần tạo làm child của GameManager)
   - Chọn `Create Empty`
   - Đặt tên: `AudioManager`

   **Lưu ý:**

   - Bạn có thể tạo AudioManager ở **ngoài** (root level của Hierarchy) - đây là cách đơn giản nhất
   - KHÔNG CẦN tạo làm child của GameManager
   - AudioManager sẽ tự động persist qua các scene (đã có `DontDestroyOnLoad` trong code)

2. **Add AudioManager component:**

   - Chọn GameObject `AudioManager` vừa tạo
   - Trong Inspector, click `Add Component`
   - Tìm và chọn `Audio Manager` script

3. **Configure AudioManager:**

   - **Audio Config:** Kéo AudioConfig asset (từ Bước 2) vào field này
   - **BGM Volume:** Điều chỉnh volume BGM (0-1, mặc định 0.7)
   - **SFX Volume:** Điều chỉnh volume SFX (0-1, mặc định 1.0)
   - **BGM Source:** Để trống (sẽ tự tạo)
   - **SFX Source:** Để trống (sẽ tự tạo)

4. **Thêm vào Persistent Objects (TÙY CHỌN - không bắt buộc):**

   - AudioManager **tự động** có `DontDestroyOnLoad` trong code, nên sẽ tự động persist qua các scene
   - **Nếu muốn quản lý tập trung:** Có thể thêm AudioManager vào GameManager's `Persistent Objects` array:
     - Chọn `GameManager` GameObject trong scene
     - Trong Inspector, tìm mảng `Persistent Objects`
     - Tăng `Size` lên 1 (nếu cần)
     - Kéo `AudioManager` GameObject vào slot mới
   - **Nhưng điều này KHÔNG BẮT BUỘC** - AudioManager vẫn hoạt động tốt nếu không thêm vào

---

## BƯỚC 4: KIỂM TRA VÀ TEST

1. **Kiểm tra AudioManager:**

   - Chạy game (Play mode)
   - BGM sẽ tự động phát khi game start
   - Kiểm tra Console để xem có lỗi không

2. **Test các sound effects:**

   - **Combat Hit:** Tấn công enemy (nhấn Slash button)
   - **UI Click:** Click vào inventory slot hoặc shop button
   - **Shop Open:** Mở shop (nhấn Interact khi gần shopkeeper)

3. **Điều chỉnh volume:**
   - Có thể điều chỉnh volume trong Inspector của AudioManager
   - Hoặc thêm UI slider để player điều chỉnh (tùy chọn)

---

## BƯỚC 5: TỐI ƯU HÓA (OPTIONAL)

1. **Audio Import Settings:**

   - Chọn từng audio file trong Project
   - Trong Inspector, điều chỉnh:
     - **Load Type:** `Compressed In Memory` (tiết kiệm RAM)
     - **Compression Format:** `Vorbis` (cho MP3)
     - **Quality:** 70-80% (cân bằng chất lượng và dung lượng)

2. **BGM Settings:**

   - BGM nên set:
     - **Load Type:** `Streaming` (cho file lớn)
     - **Compression Format:** `Vorbis`
     - **Quality:** 50-70%

3. **SFX Settings:**
   - SFX nên set:
     - **Load Type:** `Compressed In Memory`
     - **Compression Format:** `Vorbis`
     - **Quality:** 70-80%

---

## TROUBLESHOOTING

### BGM không phát:

- Kiểm tra AudioConfig đã được assign vào AudioManager chưa
- Kiểm tra BGM Clip đã được assign vào AudioConfig chưa
- Kiểm tra AudioListener có trong scene không (thường ở Main Camera)
- Kiểm tra volume BGM > 0

### SFX không phát:

- Kiểm tra AudioConfig đã được assign vào AudioManager chưa
- Kiểm tra SFX clips đã được assign vào AudioConfig chưa
- Kiểm tra volume SFX > 0
- Kiểm tra Console để xem có warning về missing clips không

### Audio bị delay:

- Kiểm tra Audio Import Settings (Load Type)
- Thử đổi Load Type từ `Streaming` sang `Compressed In Memory` cho SFX
- Kiểm tra DSP Buffer Size trong Project Settings > Audio

---

## CẤU TRÚC CODE

### AudioManager.cs

- Singleton pattern, tự động `DontDestroyOnLoad`
- Quản lý 2 AudioSource: BGM (loop) và SFX (one-shot)
- Methods:
  - `PlayBGM()`: Phát background music
  - `PlaySFX(string name)`: Phát SFX theo tên
  - `PlayCombatHit()`, `PlayUIClick()`, `PlayShopOpen()`: Helper methods

### AudioConfig.cs

- ScriptableObject chứa audio clips
- Có thể mở rộng với `Additional SFX` list
- Methods:
  - `GetSFXClip(string name)`: Lấy clip theo tên

---

## MỞ RỘNG (NẾU CẦN)

Để thêm audio mới:

1. **Thêm vào AudioConfig:**

   - Thêm field mới trong AudioConfig (nếu là predefined)
   - Hoặc thêm vào `Additional SFX` list với name và clip

2. **Thêm method vào AudioManager (optional):**

   ```csharp
   public void PlayNewSound()
   {
       PlaySFX("NewSoundName");
   }
   ```

3. **Gọi trong code:**
   ```csharp
   if (AudioManager.Instance != null)
   {
       AudioManager.Instance.PlaySFX("NewSoundName");
   }
   ```

---

## LƯU Ý

- AudioManager là singleton, chỉ cần 1 instance trong toàn bộ game
- AudioManager tự động persist qua các scene
- Nếu không có AudioConfig, audio sẽ không phát (sẽ có warning trong Console)
- Tất cả audio calls đều có null check để tránh lỗi nếu AudioManager chưa được setup

---

**Chúc bạn setup thành công! 🎵**
