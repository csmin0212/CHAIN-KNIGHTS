namespace ChainKnights.Core.Cards
{
    /// <summary>카드 한 장의 고정 데이터. 플레이어가 가진 카드의 레벨이나 명성은 따로 저장한다.</summary>
    public sealed class CardDefinition
    {
        /// <summary>원작 위키의 카드 수집 번호.</summary>
        public int Id { get; }
        public string NameKo { get; }
        public string NameJp { get; }
        public Job Job { get; }
        public Element Element { get; }

        /// <summary>성 (1~5). 원작 N, HN, R, SR, UR에 대응한다.</summary>
        public int Rarity { get; }
        public int Cost { get; }
        public SkillCategory Category { get; }
        public CardSkill Normal { get; }

        /// <summary>각성 스킬. 직업이 없는 카드는 null.</summary>
        public CardSkill Awakened { get; }
        public CardSource Source { get; }

        /// <summary>성마다 최대 레벨이 정해져 있다: ★1 = 10, ★2 = 20 … ★5 = 50.</summary>
        public int MaxLevel => Rarity * 10;

        public CardDefinition(
            int id,
            string nameKo,
            string nameJp,
            Job job,
            Element element,
            int rarity,
            int cost,
            SkillCategory category,
            CardSkill normal,
            CardSkill awakened,
            CardSource source)
        {
            Id = id;
            NameKo = nameKo;
            NameJp = nameJp;
            Job = job;
            Element = element;
            Rarity = rarity;
            Cost = cost;
            Category = category;
            Normal = normal;
            Awakened = awakened;
            Source = source;
        }

        /// <summary>이 카드를 쓰는 아서의 직업이 카드의 직업 타입과 같으면 각성한다.</summary>
        public bool IsAwakenedFor(Job arthurJob)
        {
            return Awakened != null && Job != Job.None && Job == arthurJob;
        }

        public CardSkill SkillFor(Job arthurJob)
        {
            return IsAwakenedFor(arthurJob) ? Awakened : Normal;
        }
    }
}
