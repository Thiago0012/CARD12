using UnityEngine;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Seletor exclusivamente visual para consultar os layouts serializados
    /// da cena DeckEditor sem iniciar o jogo.
    /// </summary>
    [ExecuteAlways]
    public sealed class DeckEditorLayoutPreviewSelector : MonoBehaviour
    {
        public enum PreviewLayout
        {
            ListaDeDecks = 0,
            DetalhesDoDeck = 1,
            EditorCompleto = 2
        }

        [SerializeField]
        private PreviewLayout layoutVisivel =
            PreviewLayout.EditorCompleto;
        [SerializeField]
        private GameObject listaDeDecks;
        [SerializeField]
        private GameObject detalhesDoDeck;
        [SerializeField]
        private GameObject editorCompleto;

        public PreviewLayout LayoutVisivel
        {
            get => layoutVisivel;
            set
            {
                layoutVisivel = value;
                AtualizarVisibilidade();
            }
        }

        public void Configurar(
            GameObject lista,
            GameObject detalhes,
            GameObject editor)
        {
            listaDeDecks = lista;
            detalhesDoDeck = detalhes;
            editorCompleto = editor;
            AtualizarVisibilidade();
        }

        private void OnEnable()
        {
            AtualizarVisibilidade();
        }

        private void OnValidate()
        {
            AtualizarVisibilidade();
        }

        public void AtualizarVisibilidade()
        {
            if (Application.isPlaying)
                return;
            if (listaDeDecks != null)
            {
                listaDeDecks.SetActive(
                    layoutVisivel ==
                    PreviewLayout.ListaDeDecks);
            }
            if (detalhesDoDeck != null)
            {
                detalhesDoDeck.SetActive(
                    layoutVisivel ==
                    PreviewLayout.DetalhesDoDeck);
            }
            if (editorCompleto != null)
            {
                editorCompleto.SetActive(
                    layoutVisivel ==
                    PreviewLayout.EditorCompleto);
            }
        }
    }
}
