# Hall of Us - XR Memory App  
### HackGT 2025 Submission  

A mixed reality app that transforms physical spaces into **interactive memory galleries** using spatial anchors and real-time photo synchronization.  

---

## 📖 Overview  
Hall of Us lets users place **persistent picture frames** in their environment. These frames auto-populate with photos from a shared web platform, intelligently matched by **tags** and **orientation**—blending digital memories with physical space.  

---

## ✨ Key Features  

### 🖼 Spatial Photo Frames  
- **Persistent Anchors** – Frames stay in place between sessions  
- **Smart Matching** – Photos assigned by tags + orientation (vertical/horizontal)  
- **Proximity Interaction** – Extra content appears when approaching frames  

### 🔄 Real-Time Sync  
- **API Integration** – [Photo API](https://api.doubleehbatteries.com/photos)  
- **AWS S3** – Selective image downloads (avoids re-fetching)  
- **Live Updates** – Manual trigger for demos (Right Stick Up + Trigger)  

### 🎮 VR Interaction  
- **Anchor Mode** – Hold **Right Thumbstick Down**  
- **Frame Selection** – Cycle with **X**  
- **Placement** – **Trigger** to place | **B** delete last | **Grip** clear all  
- **Refresh Photos** – **Thumbstick Up + Trigger**  

---

## 🛠 Technical Architecture  

- **SpatialAnchorManager** – Frame placement + persistence  
- **PhotoManager** – API + photo data handling  
- **S3 Integration** – Efficient downloading  
- **PhotoAnchorMatcher** – Tag + orientation-based matching  

### Smart Filtering  
- Orientation-aware (vertical/horizontal)  
- Tag-based assignment  
- Auto-ignore plaque files (`P_*`)  
- Preview object filtering (anchors with parents)  

---

## ⚙️ Setup Requirements  
- Unity w/ **Meta XR SDK**  
- AWS S3 credentials  
- Accessible API endpoint  
- VR headset (**Meta Quest recommended**)  

---

## 🌐 Integration  
Works with a companion **web platform** where users can upload + tag photos, enabling **collaborative memory sharing** across physical + digital spaces.  
https://github.com/reeyank/hall-of-us
