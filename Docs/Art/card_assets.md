# 카드 아트 1차 제작
기본 모티브는 은빛 기사단 문장, 남색 스테인드글라스, 빛의 사슬로 이어지는 원탁이다.

## 파일
| 파일 (Assets/Art/Cards/) | 크기 | 용도 |
|---|---|---|
| card_frame.png | 300×420 | 은빛 사슬 테두리. 중앙 투명. 흰색 계열로 속성 tint 가능 |
| card_back.png | 300×420 | 남색 원탁 문양 카드 뒷면 |
| card_cost_badge.png | 80×80 | 숫자를 코드로 올리는 흰색 계열 원형 배지 |
| icon_star.png | 32×32 | 별 개수와 tint를 코드로 처리 |
| card_awaken_glow.png | 300×420 | 중앙이 투명한 금빛 각성 오버레이 |

## Unity 사용
- PNG별 Sprite (Single), alpha transparency, mipmap 해제, Bilinear, Clamp, 비압축 설정을 .meta에 포함했다.
- 카드 틀의 9-slice border는 좌/하/우/상 각 50px. UI Image의 Type을 Sliced로 지정한다.
- 카드 뒷면과 각성 효과는 Image Type Simple, 같은 RectTransform 크기로 사용한다. 카드 앞면의 속성 색면과 직업 아이콘은 별도 레이어다.
- 각성 효과는 일반 알파 블렌딩으로 겹친다. 색상은 이미 금빛이므로 Image.color는 흰색을 기본으로 두고 알파로 강도를 조절한다.
- 숫자·이름·설명·별 배치는 포함하지 않았다.
- 파일 교체 시 기존 .meta를 유지해야 Unity 참조가 보존된다.

## 제작 및 확인
- 기본 제공 image_gen으로 개별 생성했다. 외부 무료 에셋을 사용하지 않았다.
- 생성 프롬프트 원문은 card_assets_prompts.json에 보관했다.
- 생성 원본에서 요청 크기로 축소 내보냈으며 RGBA와 투명 픽셀을 확인했다. 틀과 각성 효과의 중앙 alpha는 0이다.
- Unity 에디터 안에서 실제 UI 배치와 9-slice 렌더링은 아직 확인하지 않았다.

