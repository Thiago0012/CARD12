using ArcaneDuel.Game.Competitive;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        public RankPlayerSnapshot CaptureRankPlayerSnapshot()
        {
            EnsureRankRepository();
            return _repository?.CaptureRankSnapshot();
        }

        public DuelIdentitySnapshot CaptureLobbyIdentitySnapshot()
        {
            EnsureRankRepository();
            return _repository?.CaptureDuelIdentitySnapshot();
        }

        public RankPresentationModel GetRankPresentation()
        {
            EnsureRankRepository();
            return _repository != null
                ? _repository.GetRankPresentation()
                : new RankPresentationModel(new PlayerRankData());
        }

        public bool TryApplyRankReceipt(
            RankChangeReceipt proposed,
            out RankChangeReceipt receipt,
            out string rejection)
        {
            EnsureRankRepository();
            if (_repository == null)
            {
                receipt = null;
                rejection = "O perfil local não está disponível para salvar o ranque.";
                return false;
            }
            return _repository.TryCommitRankReceipt(
                proposed,
                out receipt,
                out rejection);
        }

        private void EnsureRankRepository()
        {
            if (_repository != null)
                return;
            ResolveProjectReferences();
            _repository = new DeckRepository();
            _repository.Load(_catalog);
            InitializeCoinRewardAuthorization();
        }
    }
}
