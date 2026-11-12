# 춘동이 메인 화면 엿보기 애니메이션

- 현재 표시용 파일: `Assets/Resources/Environment/MainMenu/ChundongiPeekAtlas_Pixel.png`
- 사용자 수동 수정 원본: `Assets/Resources/Environment/MainMenu/ChundongiPeekAtlas.png` (덮어쓰지 않고 보존).
- 현재 표시용 파일은 수동 수정 원본의 복사본. 새로 생성한 입체 시트 및 이번에 시험 생성한 시트 대신, 얼굴·색·무늬를 보존한 평면 원본을 최종 선택.
- Unity 원화 가져오기는 원본 1254×1254를 유지. 첫 표시 시 GPU Point 샘플링으로 128×128 시트 / 프레임당 32×32를 한 번 생성. Importer 축소의 색 보간을 피함. Point 필터, Clamp, 압축·밉맵 없음.
- 배경 원본 좌표에서 표시 영역 128×128 → 80×80(가로·세로 37.5% 감소). 오른쪽 뿌리에 맞춰 위치 수정.
- 축소 후 나무와의 틈 보정: 표시 원점 X를 1335 → 1347로 옮기고 오른쪽 뿌리 가림 윤곽도 실제 배경에 맞춰 수정. 위치와 가림 모두 배경 원본 좌표를 사용해 화면 비율 변화에도 함께 움직임.
- 원본 색상 그대로, 추가 채도 감소 없음. 사용자 얼굴 무늬도 복사본에 유지.
- 생성형 그림의 디테일을 늘리는 대신 낮은 해상도 표시로 바꾼 수정. 그림체 일치를 자동 테스트로 판정했다는 의미는 아님.
- 등장 1.08초, 관찰 1.6~2.8초, 숨기 0.6초, 숨은 뒤 7~17초 무작위 대기.

## 생성 도구 및 시안 기록

기존 원화는 built-in `image_gen`으로 생성 후 사용자가 수정. 이번에도 아래 프롬프트로 평면 도트 시안을 생성했으나, 최종 비교 후 사용자 수정 원본을 표시용 복사본으로 채택. 추가 이미지 편집은 하지 않음. 게임 내 도트 해상도는 CreateMenuPixelAtlas에서 원본 최근접 샘플링으로 적용.

Production FLAT 2D PIXEL ART animation sprite sheet: Chundongi the cream/gold ninja kitten wearing a PURPLE hood and RED scarf, shyly peeking to the left from behind an imaginary tree trunk on the right. Real low-resolution dot art. No 3D, no voxels, no block surfaces, no sculpted cloth, no painterly texture.
Image1 is the background STYLE reference: the square flat 2D pixels of the forest. Image2 is the ORIGINAL ninja cat identity/color reference only. Do not draw scenery.
CRITICAL LOGICAL RESOLUTION: total sheet exactly128x128 logical pixels, with4columns x4rows of32x32 pixel animation cells, enlarged8x to1024x1024. Every logical pixel is a single flat solid color square; hard nearest-neighbor stepping. Limit whole character palette to about18 colors. No antialiasing, no soft-alpha edges, no gradients. On each32x32 cell the character is only about20pixels wide and25pixels tall. Face only12pixels wide, each eye2pixels max, singlepixel pink nose. Deliberately small game-world sprite, not large detailed illustration.
Colors: keep PURPLE and RED saturation vivid, but use LIGHTER middle values: purple cloth around #A155C4, one soft blue-purple shadow around #76528F, violet highlight #BB78D6; bright red scarf #D66167 with one simple shade; golden cream face #F4D49B with light tan shading. Avoid near-black/navy large dark masses, thick black outline, dense high-contrast shading. Simple 2D plane shading and light golden ambient light like the forest, crisp square pixels. Do NOT desaturate to grey/brown. No excessive white shine. No added face markings.
Each cell has the same framing: invisible vertical tree edge at local x24, baseliney29, feetx26. When hidden all parts mostlyx24..30. At full peek headcenterx19,y12, pawsatx24,y20and23, feetstayx26,y29. Whole sprite stays within each cell, tree itself must NOT appear. Draw character on genuine transparent alpha.
16 consistent poses reading left-to-right then top-to-bottom:
row1: tucked; head rises; paw reaches; face starts peeking.
row2: head leans; both paws hold edge; full peek; settle.
row3: watch; closed-eye blink; open eyes; recoil.
row4: halfway tucked; paw releases; almost hidden; tucked again.
No text, borders, gridlines, background. A TRUE32x32 low-resolution pixel animation, no faux pixel highresolution painting.


