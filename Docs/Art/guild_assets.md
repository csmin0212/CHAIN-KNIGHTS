# 모험가 길드 에셋 제작 기록

## 디자인
짙은 차콜 바탕, 크림색 양피지, 얇은 황동 테두리, 가죽과 나무를 공통 재질로 사용했다.
나침반과 작은 사슬 고리를 길드 문장으로 사용하고 장식은 모서리와 핵심 표시에 집중했다.
팔레트는 기존 전투 목업과 동일하다: #17181f, #1f2129, #2a2c36, #c9a96e, #efe3c8.
불 #d9583b / 얼음 #4c9bd6 / 바람 #4fa36b / 빛 #e3b53b / 어둠 #8a72c4.

## 납품 현황
PNG 75개와 사운드 14개를 제작했다. 카드 일러스트 84장은 제작 대상에서 제외했다.

| 폴더 (Assets/Resources/Art 아래) | PNG 수 | 내용 |
|---|---:|---|
| Cards | 5 | 길드 카드 몸통·뒷면·코스트 배지, 별, 각성 빛 |
| Icons | 28 | 속성 5, 직업 4, 스킬 6, 신호 6, 상태 7 |
| Arthurs | 8 | 직업별 초상화 4, 걷기 시트 4 |
| Enemies | 9 | 구골, 기사 3, 빙결룡 부위 3, 요정 2 |
| Backgrounds | 6 | 시작의 대지, 얼음 동굴, 로비, 타이틀, 숲, 유적 |
| UI | 11 | 패널 2, 버튼 3, HP 2, 타이머, 준비 표시, 말풍선, 로고 |
| Effects | 5 | 타격, 회복, 부위 예고, 체인, 크리티컬 |
| Lobby | 1 | 의뢰 게시판 |
| Gacha | 2 | 상자와 개봉 빛 |

별과 각성 빛은 앞서 제작한 중립색·금빛 파일을 재사용했다.
이전 은빛 카드 시안은 Assets/Art/Cards에 보관되어 있으며, 게임 자동 로딩용 최종 경로는 Assets/Resources/Art다.

## 미리보기
- [카드](GuildPreviews/cards.png)
- [아이콘](GuildPreviews/icons.png)
- [아서와 걷기 시트](GuildPreviews/arthurs.png)
- [적](GuildPreviews/enemies.png)
- [배경](GuildPreviews/backgrounds.png)
- [UI](GuildPreviews/ui.png)
- [전투 이펙트](GuildPreviews/effects.png)
- [게시판](GuildPreviews/lobby.png)
- [뽑기](GuildPreviews/gacha.png)

## Unity 사용
- 스프라이트는 Single, Full Rect, Bilinear, Clamp, mipmap 해제, 비압축을 기본으로 한다.
- 배경 6장은 전체가 불투명하며, 나머지는 RGBA 투명 PNG다.
- 카드·패널·버튼·HP바는 9-slice border를 설정한다. UI Image.Type = Sliced로 사용한다.
- 카드 각성 빛과 이펙트는 일반 알파 블렌딩으로 겹친다.
- 걷기는 512×128 시트, 128×128 프레임 4개, 오른쪽을 보고 걷는다. 왼쪽은 반전해서 쓴다.
- Editor 메뉴 Chain Knights > Art > Configure and verify guild assets는 임포트 설정, 걷기 분할, 8fps 좌우 반복 AnimationClip 생성을 수행한다.
- AnimationClip은 Arthurs/Animations 아래에 생성되며 SpriteRenderer를 대상으로 한다. Canvas UI에서 쓰려면 프레임 배열을 UI Image에 적용하는 별도 연결이 필요하다.
- 글라키에스 본체·발톱·날개는 개별 타깃용 분리 그림이다. 끊김 없는 스켈레톤 리깅 원본은 아니다.
- 현재 코드에 자동 로딩이 연결된 범위는 카드 몸통·뒷면, 초상화, 구골, 전투 배경, 글라키에스 부위다. 다른 UI·아이콘·이펙트·사운드의 실제 게임 연결은 별도 개발 작업이다.
- 에셋 교체 시 기존 .meta를 보존해야 GUID 참조가 유지된다.

## 사운드
Assets/Resources/Audio에 WAV 효과음 11개와 OGG BGM 3곡을 저장했다.
외부 샘플 없이 직접 합성한 첫 제작본이며, 최종 음량 밸런스와 게임 내 청취 검수는 별도로 필요하다.

| 파일 | 내용 |
|---|---|
| bgm_battle.ogg | 112 BPM 일반 전투, 16마디 반복 |
| bgm_boss.ogg | 128 BPM 보스전, 16마디 반복 |
| bgm_lobby.ogg | 90 BPM 로비, 16마디 반복 |
| sfx_card_play.wav | 카드 내기 |
| sfx_hit.wav | 타격 |
| sfx_magic.wav | 마법 |
| sfx_heal.wav | 회복 |
| sfx_buff.wav | 버프 |
| sfx_chain.wav | 체인 |
| sfx_critical.wav | 크리티컬 |
| sfx_part_break.wav | 부위 파괴 |
| sfx_victory.wav | 승리 |
| sfx_defeat.wav | 패배 |
| sfx_button.wav | 버튼 |

효과음은 44.1kHz mono 16-bit PCM WAV, BGM은 44.1kHz stereo Vorbis OGG다.
BGM은 음의 잔향을 구간 처음으로 감아 연결하는 방식으로 루프 경계를 설계했다. 재생 시 AudioSource.loop를 사용한다.

## 검증 결과
- 파일 검사: PNG 75개, 오디오 14개, 누락·크기·투명도·디코딩 오류 0개.
- Unity 6000.3.17f1 배치 검사: 텍스처 75개, 스프라이트 87개, 오디오 14개, 좌우 걷기 클립 8개, 오류 0개.
- 걷기 시트는 각 직업별 4개 프레임에 캐릭터가 모두 포함되며 바닥 위치가 일정함을 확인했다.
- 실제 게임 플레이 중 UI 배치·전투 이펙트·오디오 밸런스는 아직 확인하지 않았다.

## 제작 도구와 원본 기록
- 그래픽: 기본 제공 image_gen. 프롬프트 원문은 guild_assets_manifest.json, 생성 원본 경로는 guild_asset_sources.json에 기록했다.
- 프롬프트 공통 방향: 절제된 판타지 모험가 길드, 차콜·양피지·황동, 읽기 쉬운 형태, 작은 장식, 글자 없는 개별 투명 스프라이트. 배경은 불투명, 로고만 지정한 영문 제목을 포함한다.
- 크기 변환·PNG 내보내기: Tools/ArtPipeline/export_guild_art.ps1.
- 미리보기: Tools/ArtPipeline/preview_guild_art.ps1.
- 오디오 합성과 OGG 변환: Tools/ArtPipeline/create_guild_audio.py, numpy/scipy/ffmpeg.
- 검사: Tools/ArtPipeline/verify_guild_assets.py, guild_delivery_audit.json.
- Unity 검사: Assets/Editor/GuildArtTools/GuildArtImport.cs, guild_unity_import_report.json.
- 외부 무료 에셋이나 타인의 음원 샘플은 사용하지 않았다. AI 생성물이며 별도 제3자 라이선스가 있는 에셋으로 표시하지 않는다.

