# Plan 2: Player Web Panel - Test Checklist

## Tổng quan

File này chứa checklist để test các tính năng của Player Web Panel đã được implement trong Plan 2.

## Prerequisites

- Server đã được start và chạy (thường là `http://localhost:5220`)
- Database đã có ít nhất một PlayerProfile (có thể tạo từ game client hoặc admin panel)
- Browser để test web interface

---

## 1. Authentication System (Session-based)

### 1.1 Login Page

- [ ] **Test Case 1.1.1**: Truy cập `/Player/Login` khi chưa đăng nhập

  - **Expected**: Hiển thị login form với input "Player Name"
  - **Steps**:
    1. Mở browser, navigate đến `http://localhost:5220/Player/Login`
    2. Kiểm tra form có input field và submit button

- [ ] **Test Case 1.1.2**: Login với player name hợp lệ

  - **Expected**: Redirect đến `/Player` (Dashboard) sau khi login thành công
  - **Steps**:
    1. Nhập player name đã tồn tại trong database
    2. Click "Login"
    3. Kiểm tra redirect đến Dashboard
    4. Kiểm tra session có chứa PlayerId và PlayerName

- [ ] **Test Case 1.1.3**: Login với player name không tồn tại

  - **Expected**: Hiển thị error message "Player '{name}' not found"
  - **Steps**:
    1. Nhập player name không tồn tại (ví dụ: "NonExistentPlayer123")
    2. Click "Login"
    3. Kiểm tra error message hiển thị

- [ ] **Test Case 1.1.4**: Login với player name rỗng

  - **Expected**: Hiển thị error "Player name is required."
  - **Steps**:
    1. Để trống input field
    2. Click "Login"
    3. Kiểm tra validation error

- [ ] **Test Case 1.1.5**: Truy cập `/Player/Login` khi đã đăng nhập
  - **Expected**: Tự động redirect đến Dashboard
  - **Steps**:
    1. Đăng nhập thành công
    2. Navigate đến `/Player/Login` lại
    3. Kiểm tra tự động redirect

### 1.2 Logout

- [ ] **Test Case 1.2.1**: Logout từ navbar

  - **Expected**: Clear session và redirect đến Login page
  - **Steps**:
    1. Đăng nhập thành công
    2. Click "Logout" button ở navbar
    3. Kiểm tra redirect đến `/Player/Login`
    4. Kiểm tra session đã bị clear (không thể truy cập Dashboard nữa)

- [ ] **Test Case 1.2.2**: Truy cập `/Player/Logout` trực tiếp
  - **Expected**: Auto-submit form và logout
  - **Steps**:
    1. Đăng nhập thành công
    2. Navigate đến `/Player/Logout`
    3. Kiểm tra auto-redirect đến Login

### 1.3 Session Management

- [ ] **Test Case 1.3.1**: Truy cập protected page khi chưa đăng nhập

  - **Expected**: Redirect đến Login page
  - **Steps**:
    1. Clear cookies/session
    2. Truy cập `/Player` hoặc `/Player/Profile`
    3. Kiểm tra redirect đến Login

- [ ] **Test Case 1.3.2**: Session timeout
  - **Expected**: Sau khi session expire, phải login lại
  - **Steps**:
    1. Đăng nhập thành công
    2. Đợi session timeout (8 hours) hoặc clear session manually
    3. Truy cập protected page
    4. Kiểm tra redirect đến Login

---

## 2. Dashboard Page (`/Player`)

### 2.1 Basic Display

- [ ] **Test Case 2.1.1**: Hiển thị Dashboard sau khi login

  - **Expected**: Hiển thị đầy đủ thông tin player
  - **Steps**:
    1. Đăng nhập thành công
    2. Kiểm tra Dashboard hiển thị:
       - Player name ở header
       - 4 stat cards: Level, Gold, Total Matches, Play Time
       - Experience progress bar
       - Quick stats section
       - Recent activity section

- [ ] **Test Case 2.1.2**: Stat cards hiển thị đúng giá trị
  - **Expected**: Các giá trị match với database
  - **Steps**:
    1. Kiểm tra Level card hiển thị đúng level của player
    2. Kiểm tra Gold card hiển thị đúng số gold
    3. Kiểm tra Total Matches hiển thị đúng số matches
    4. Kiểm tra Play Time hiển thị đúng format (hours/minutes)

### 2.2 Experience Progress

- [ ] **Test Case 2.2.1**: Progress bar hiển thị đúng
  - **Expected**: Progress bar tính đúng percentage
  - **Steps**:
    1. Kiểm tra progress bar hiển thị: `Exp / ExpToLevel`
    2. Kiểm tra percentage tính đúng: `(Exp / ExpToLevel) * 100`
    3. Kiểm tra progress bar có màu xanh (bg-success)

### 2.3 Navigation Links

- [ ] **Test Case 2.3.1**: Links trong Dashboard hoạt động
  - **Expected**: Các link navigate đúng trang
  - **Steps**:
    1. Click "View Full Stats" → Navigate đến `/Player/Stats`
    2. Click "View Match History" → Navigate đến `/Player/History`

---

## 3. Profile Page (`/Player/Profile`)

### 3.1 Basic Information Section

- [ ] **Test Case 3.1.1**: Hiển thị thông tin cơ bản

  - **Expected**: Hiển thị đầy đủ thông tin player
  - **Steps**:
    1. Navigate đến `/Player/Profile`
    2. Kiểm tra hiển thị:
       - Player Name
       - Player ID (dạng code)
       - Level (badge)
       - Experience với progress bar
       - Gold (với icon)
       - Account Created date

- [ ] **Test Case 3.1.2**: Experience progress bar
  - **Expected**: Giống như Dashboard
  - **Steps**:
    1. So sánh progress bar với Dashboard
    2. Kiểm tra giá trị match

### 3.2 Statistics Section

- [ ] **Test Case 3.2.1**: Hiển thị stats chi tiết
  - **Expected**: Hiển thị đầy đủ stats
  - **Steps**:
    1. Kiểm tra table hiển thị:
       - Damage
       - Range
       - Knockback Force
       - Speed
       - Max Health
       - Current Health
    2. Kiểm tra link "View Detailed Stats" navigate đến `/Player/Stats`

### 3.3 Inventory & Skills

- [ ] **Test Case 3.3.1**: Hiển thị inventory count

  - **Expected**: Hiển thị số lượng items
  - **Steps**:
    1. Kiểm tra card "Inventory" hiển thị đúng số items
    2. Kiểm tra icon và styling

- [ ] **Test Case 3.3.2**: Hiển thị skills count
  - **Expected**: Hiển thị số lượng skills unlocked
  - **Steps**:
    1. Kiểm tra card "Skills" hiển thị đúng số skills
    2. Kiểm tra icon và styling

---

## 4. Stats Page (`/Player/Stats`)

### 4.1 Combat Stats

- [ ] **Test Case 4.1.1**: Hiển thị combat stats
  - **Expected**: Hiển thị Damage, Range, Knockback Force với badges
  - **Steps**:
    1. Navigate đến `/Player/Stats`
    2. Kiểm tra card "Combat Stats" hiển thị:
       - Damage (badge bg-danger)
       - Range (badge bg-info)
       - Knockback Force (badge bg-warning)
    3. Kiểm tra giá trị match với database

### 4.2 Movement Stats

- [ ] **Test Case 4.2.1**: Hiển thị movement stats
  - **Expected**: Hiển thị Speed
  - **Steps**:
    1. Kiểm tra card "Movement Stats" hiển thị Speed
    2. Kiểm tra giá trị match với database

### 4.3 Health Stats

- [ ] **Test Case 4.3.1**: Hiển thị health stats với progress bar
  - **Expected**: Hiển thị Max Health, Current Health, và Health Percentage
  - **Steps**:
    1. Kiểm tra card "Health Stats" hiển thị:
       - Max Health (badge bg-danger)
       - Current Health (badge bg-success)
       - Health Percentage với progress bar
    2. Kiểm tra progress bar tính đúng: `(CurrentHealth / MaxHealth) * 100`

### 4.4 Player Info

- [ ] **Test Case 4.4.1**: Hiển thị player info
  - **Expected**: Hiển thị Name, Level, Experience
  - **Steps**:
    1. Kiểm tra card "Player Info" hiển thị đúng thông tin
    2. Kiểm tra experience progress bar

---

## 5. History Page (`/Player/History`)

### 5.1 Match History Table

- [ ] **Test Case 5.1.1**: Hiển thị match history

  - **Expected**: Hiển thị table với các matches
  - **Steps**:
    1. Navigate đến `/Player/History`
    2. Kiểm tra table hiển thị các columns:
       - Session ID (truncated)
       - Start Time
       - End Time
       - Duration
       - Players (badge)
       - Status (badge với màu phù hợp)
       - Your Play Time
    3. Kiểm tra data match với database

- [ ] **Test Case 5.1.2**: Format dates và times

  - **Expected**: Dates hiển thị đúng format "yyyy-MM-dd HH:mm:ss"
  - **Steps**:
    1. Kiểm tra Start Time format
    2. Kiểm tra End Time format (hoặc "-" nếu null)
    3. Kiểm tra Duration format (minutes và seconds)

- [ ] **Test Case 5.1.3**: Status badges

  - **Expected**: Status badges có màu phù hợp
  - **Steps**:
    1. Kiểm tra "Completed" → badge bg-success (xanh)
    2. Kiểm tra "Active" → badge bg-primary (xanh dương)
    3. Kiểm tra các status khác → badge bg-secondary (xám)

- [ ] **Test Case 5.1.4**: Play Duration format
  - **Expected**: Hiển thị đúng format "X m Y s" hoặc "-" nếu null
  - **Steps**:
    1. Kiểm tra format cho các matches có PlayDuration
    2. Kiểm tra hiển thị "-" cho matches không có PlayDuration

### 5.2 Pagination

- [ ] **Test Case 5.2.1**: Pagination hoạt động

  - **Expected**: Có thể navigate giữa các pages
  - **Steps**:
    1. Nếu có nhiều hơn 20 matches, kiểm tra pagination hiển thị
    2. Click "Next" → Navigate đến page 2
    3. Click "Previous" → Navigate về page 1
    4. Click số page → Navigate đến page đó
    5. Kiểm tra "Previous" disabled ở page 1
    6. Kiểm tra "Next" disabled ở page cuối

- [ ] **Test Case 5.2.2**: Pagination với ít matches
  - **Expected**: Không hiển thị pagination nếu <= 20 matches
  - **Steps**:
    1. Với player có <= 20 matches
    2. Kiểm tra pagination không hiển thị

### 5.3 Empty State

- [ ] **Test Case 5.3.1**: Hiển thị empty state khi không có matches
  - **Expected**: Hiển thị message và link về Dashboard
  - **Steps**:
    1. Với player chưa có matches
    2. Kiểm tra hiển thị message "No match history found"
    3. Kiểm tra link "Go to Dashboard" hoạt động

---

## 6. Results Page (`/Player/Results`)

### 6.1 Placeholder Display

- [ ] **Test Case 6.1.1**: Hiển thị placeholder message
  - **Expected**: Hiển thị message về Plan 3
  - **Steps**:
    1. Navigate đến `/Player/Results`
    2. Kiểm tra hiển thị message "Game Results feature is coming soon!"
    3. Kiểm tra giải thích về Plan 3 dependency
    4. Kiểm tra link "View Match History Instead" hoạt động

---

## 7. Layout & Navigation

### 7.1 Sidebar Navigation

- [ ] **Test Case 7.1.1**: Sidebar hiển thị đúng

  - **Expected**: Hiển thị các menu items với icons
  - **Steps**:
    1. Kiểm tra sidebar hiển thị:
       - Dashboard (icon: tachometer-alt)
       - Profile (icon: user)
       - Stats (icon: chart-bar)
       - Match History (icon: history)
       - Game Results (icon: trophy)
    2. Kiểm tra icons hiển thị đúng

- [ ] **Test Case 7.1.2**: Active state highlighting

  - **Expected**: Menu item hiện tại được highlight
  - **Steps**:
    1. Navigate đến từng page
    2. Kiểm tra menu item tương ứng có class "active"
    3. Kiểm tra border-left-color và background-color

- [ ] **Test Case 7.1.3**: Navigation links hoạt động
  - **Expected**: Click vào menu item navigate đúng trang
  - **Steps**:
    1. Click từng menu item
    2. Kiểm tra navigate đúng trang

### 7.2 Navbar

- [ ] **Test Case 7.2.1**: Navbar hiển thị player name

  - **Expected**: Hiển thị player name và logout button
  - **Steps**:
    1. Kiểm tra navbar hiển thị player name
    2. Kiểm tra logout button hiển thị
    3. Kiểm tra logout button hoạt động

- [ ] **Test Case 7.2.2**: Brand link
  - **Expected**: Click brand link navigate đến Dashboard
  - **Steps**:
    1. Click "Player Panel" brand
    2. Kiểm tra navigate đến `/Player`

### 7.3 Responsive Design

- [ ] **Test Case 7.3.1**: Mobile responsive
  - **Expected**: Layout responsive trên mobile
  - **Steps**:
    1. Resize browser window xuống mobile size
    2. Kiểm tra sidebar collapse đúng cách
    3. Kiểm tra content vẫn readable

---

## 8. API Endpoints (Optional)

### 8.1 Profile API

- [ ] **Test Case 8.1.1**: GET `/player/api/playerapi/profile`

  - **Expected**: Trả về JSON profile data
  - **Steps**:
    1. Đăng nhập thành công
    2. Gửi GET request đến endpoint (có session cookie)
    3. Kiểm tra response JSON có đầy đủ fields
    4. Kiểm tra values match với database

- [ ] **Test Case 8.1.2**: Unauthorized access
  - **Expected**: Trả về 401 Unauthorized
  - **Steps**:
    1. Không đăng nhập (hoặc clear session)
    2. Gửi GET request
    3. Kiểm tra response 401 với error message

### 8.2 Stats API

- [ ] **Test Case 8.2.1**: GET `/player/api/playerapi/stats`
  - **Expected**: Trả về JSON stats data
  - **Steps**:
    1. Đăng nhập thành công
    2. Gửi GET request
    3. Kiểm tra response JSON có đầy đủ stats fields
    4. Kiểm tra values match với database

### 8.3 History API

- [ ] **Test Case 8.3.1**: GET `/player/api/playerapi/history?page=1&pageSize=20`
  - **Expected**: Trả về JSON history với pagination info
  - **Steps**:
    1. Đăng nhập thành công
    2. Gửi GET request với query params
    3. Kiểm tra response có:
       - `history` array
       - `totalMatches`
       - `currentPage`
       - `pageSize`
       - `totalPages`
    4. Kiểm tra pagination tính đúng

### 8.4 Results API

- [ ] **Test Case 8.4.1**: GET `/player/api/playerapi/results`
  - **Expected**: Trả về placeholder message
  - **Steps**:
    1. Đăng nhập thành công
    2. Gửi GET request
    3. Kiểm tra response có message về Plan 3
    4. Kiểm tra `results` là empty array

---

## 9. Integration Tests

### 9.1 End-to-End Flow

- [ ] **Test Case 9.1.1**: Complete user flow
  - **Expected**: User có thể navigate qua tất cả pages
  - **Steps**:
    1. Login → Dashboard
    2. Click "Profile" → Profile page
    3. Click "Stats" → Stats page
    4. Click "Match History" → History page
    5. Click "Game Results" → Results page
    6. Click "Dashboard" → Dashboard
    7. Logout → Login page

### 9.2 Data Consistency

- [ ] **Test Case 9.2.1**: Data consistency giữa các pages
  - **Expected**: Data hiển thị consistent
  - **Steps**:
    1. Ghi nhận Level, Gold, Exp từ Dashboard
    2. Kiểm tra Profile page hiển thị cùng values
    3. Kiểm tra Stats page hiển thị cùng stats
    4. Kiểm tra History page có đúng số matches

### 9.3 Error Handling

- [ ] **Test Case 9.3.1**: Player không tồn tại

  - **Expected**: Error handling graceful
  - **Steps**:
    1. Login với player name không tồn tại
    2. Kiểm tra error message rõ ràng
    3. Kiểm tra không crash server

- [ ] **Test Case 9.3.2**: Database connection issues
  - **Expected**: Error handling graceful
  - **Steps**:
    1. Simulate database connection issue (nếu có thể)
    2. Kiểm tra error không expose sensitive info
    3. Kiểm tra user-friendly error message

---

## 10. Performance Tests

### 10.1 Page Load Time

- [ ] **Test Case 10.1.1**: Dashboard load time

  - **Expected**: Load trong thời gian hợp lý (< 2 seconds)
  - **Steps**:
    1. Measure time từ khi click đến khi page render
    2. Kiểm tra < 2 seconds

- [ ] **Test Case 10.1.2**: History page với nhiều matches
  - **Expected**: Pagination giúp load nhanh
  - **Steps**:
    1. Với player có nhiều matches (> 100)
    2. Kiểm tra page 1 load nhanh
    3. Kiểm tra pagination hoạt động

---

## 11. Security Tests

### 11.1 Session Security

- [ ] **Test Case 11.1.1**: Session cookie security

  - **Expected**: Session cookie có HttpOnly flag
  - **Steps**:
    1. Check browser DevTools → Application → Cookies
    2. Kiểm tra `.Player.Session` cookie có HttpOnly = true

- [ ] **Test Case 11.1.2**: Session hijacking prevention
  - **Expected**: Không thể access với session ID của người khác
  - **Steps**:
    1. Login với player A
    2. Copy session ID
    3. Logout
    4. Login với player B
    5. Thử thay session ID = session của player A
    6. Kiểm tra không thể access data của player A

### 11.2 Input Validation

- [ ] **Test Case 11.2.1**: SQL Injection prevention

  - **Expected**: Input được sanitize
  - **Steps**:
    1. Thử login với player name: `'; DROP TABLE PlayerProfiles; --`
    2. Kiểm tra không execute SQL
    3. Kiểm tra error message hợp lý

- [ ] **Test Case 11.2.2**: XSS prevention
  - **Expected**: Script tags không execute
  - **Steps**:
    1. Nếu có thể, thử set player name có `<script>alert('XSS')</script>`
    2. Kiểm tra không execute script
    3. Kiểm tra được escape đúng cách

---

## Test Data Setup

### Tạo Test Players

Có thể tạo test players bằng cách:

1. Chạy game client và tạo player mới
2. Hoặc sử dụng Admin panel để tạo player
3. Hoặc insert trực tiếp vào database (không khuyến khích)

### Test Scenarios

1. **New Player**: Player mới, chưa có matches

   - Test empty states
   - Test default values

2. **Experienced Player**: Player có nhiều matches và stats

   - Test pagination
   - Test data display
   - Test performance

3. **Player with High Level**: Player level cao
   - Test experience progress
   - Test stats display

---

## Notes

- Tất cả tests nên được chạy trên development environment trước
- Nếu có lỗi, ghi lại steps để reproduce
- Test với nhiều browsers khác nhau (Chrome, Firefox, Edge)
- Test với nhiều screen sizes (desktop, tablet, mobile)

---

## Checklist Summary

- [ ] Authentication System (7 test cases)
- [ ] Dashboard Page (3 test cases)
- [ ] Profile Page (3 test cases)
- [ ] Stats Page (4 test cases)
- [ ] History Page (4 test cases)
- [ ] Results Page (1 test case)
- [ ] Layout & Navigation (3 test cases)
- [ ] API Endpoints (4 test cases)
- [ ] Integration Tests (3 test cases)
- [ ] Performance Tests (2 test cases)
- [ ] Security Tests (4 test cases)

**Total: 34 test cases**

---

## Quick Test Script

Để test nhanh, có thể chạy script sau:

```bash
# 1. Start server
cd server
dotnet run

# 2. Mở browser và test:
# - http://localhost:5220/Player/Login
# - Login với player name đã tồn tại
# - Navigate qua các pages
# - Test logout
```

---

## Known Issues / TODOs

- [ ] Game Results page chờ Plan 3 implementation
- [ ] Có thể thêm caching cho performance
- [ ] Có thể thêm real-time updates với SignalR (future enhancement)
