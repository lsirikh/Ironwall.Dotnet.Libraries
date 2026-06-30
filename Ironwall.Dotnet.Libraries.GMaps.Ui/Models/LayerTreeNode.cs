using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

/// <summary>
/// 레이어 패널 트리 노드.
/// 부모 노드(그룹)와 자식 노드(개별 레이어)의 계층 구조를 지원.
/// 3-state 체크박스: true(전체 ON), false(전체 OFF), null(일부 ON = indeterminate)
/// </summary>
public class LayerTreeNode : INotifyPropertyChanged
{
    #region Fields
    private string _name = string.Empty;
    private string _iconKind = "Layers";
    private bool? _isChecked = true;
    private double _opacity = 1.0;
    private bool _isExpanded;
    private int _itemCount;
    private bool _isUpdatingChildren;
    private bool _isUpdatingParent;
    private bool _isEditing;
    private string _editingName = string.Empty;
    private ICommand? _deleteCommand;
    private ICommand? _moveUpCommand;
    private ICommand? _moveDownCommand;
    private ICommand? _renameCommand;
    private ICommand? _commitRenameCommand;
    private ICommand? _cancelRenameCommand;
    private ICommand? _navigateCommand;
    #endregion

    #region Properties

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string IconKind
    {
        get => _iconKind;
        set { _iconKind = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 3-state: true(ON), false(OFF), null(indeterminate — 자식 중 일부만 ON)
    /// </summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            OnPropertyChanged();

            // 부모 → 자식 전파
            if (!_isUpdatingParent && Children.Count > 0 && value.HasValue)
            {
                _isUpdatingChildren = true;
                foreach (var child in Children)
                    child.IsChecked = value.Value;
                _isUpdatingChildren = false;
            }

            // 자식 → 부모 전파 (부모가 일괄 cascade 중이면 건너뜀 — 대형 카테고리 토글 시 O(n^2) 재계산 방지, FR-03)
            if (!_isUpdatingChildren && Parent != null && !Parent._isUpdatingChildren)
            {
                Parent.UpdateCheckStateFromChildren();
            }

            // DB 모델 동기화
            if (Model != null)
                Model.IsVisible = value ?? true;

            // 개별 심볼 가시성 영속 필드(ShowShape) 동기화 — 실제 마커 토글은 CheckChanged 핸들러가 수행(FR-03)
            if (Symbol != null && value.HasValue)
                Symbol.ShowShape = value.Value;

            // Leaf 노드 체크 변경 시 이벤트 발생 (개별 심볼 리프 포함)
            if (NodeType == LayerNodeType.Leaf)
                CheckChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// CheckChanged를 발화하지 않고 _isChecked를 갱신 (시작 집계 전용).
    /// 자식/부모 전파 없음 — leaf 단위 직접 적용 시만 사용.
    /// </summary>
    internal void SetCheckedSilently(bool? value)
    {
        if (_isChecked == value) return;
        _isChecked = value;
        OnPropertyChanged(nameof(IsChecked));
        // 부모 그룹/섹션 tri-state 갱신 (CheckChanged 미발화 — 그룹 노드는 Leaf가 아님)
        Parent?.UpdateCheckStateFromChildren();
    }

    /// <summary>
    /// Leaf 노드의 IsChecked가 변경될 때 발생 (routed event 대체)
    /// </summary>
    public event EventHandler? CheckChanged;
    public event EventHandler? OpacityChanged;

    public double Opacity
    {
        get => _opacity;
        set
        {
            if (Math.Abs(_opacity - value) < 0.001) return;
            _opacity = value;
            OnPropertyChanged();
            if (Model != null)
            {
                Model.Opacity = value;
                OpacityChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 해당 카테고리의 마커/아이템 개수 (런타임 업데이트)
    /// </summary>
    public int ItemCount
    {
        get => _itemCount;
        set { _itemCount = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 인라인 이름 편집 모드 여부
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 노드 유형: Section, Group, Leaf
    /// </summary>
    public LayerNodeType NodeType { get; set; } = LayerNodeType.Leaf;

    /// <summary>
    /// 오버레이 노드만 Opacity 슬라이더 표시
    /// </summary>
    public bool HasOpacitySlider { get; set; }

    /// <summary>
    /// DB 모델 참조 (Overlay Leaf / Category 노드, Group/Section/SymbolLeaf은 null)
    /// </summary>
    public IMapLayerModel? Model { get; set; }

    /// <summary>
    /// 개별 심볼 리프 전용 페이로드(ISymbolModel). Model(IMapLayerModel)과 상속관계 없으므로 별도 슬롯(FR-01 이음새).
    /// </summary>
    public ISymbolModel? Symbol { get; set; }

    /// <summary>개별 심볼 리프 여부 — 액션 게이팅·템플릿 분기·이벤트 라우팅용.</summary>
    public bool IsSymbolLeaf => Symbol != null;

    /// <summary>
    /// DB Category 값 (필터링/매핑용)
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 부모 노드 참조
    /// </summary>
    public LayerTreeNode? Parent { get; set; }

    /// <summary>
    /// 자식 노드 컬렉션
    /// </summary>
    public ObservableCollection<LayerTreeNode> Children { get; } = new();

    #endregion

    #region Methods

    /// <summary>
    /// 자식 추가 + 부모 참조 설정
    /// </summary>
    public void AddChild(LayerTreeNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    /// <summary>
    /// 자식 체크 상태로 부모 상태 갱신
    /// </summary>
    private void UpdateCheckStateFromChildren()
    {
        if (Children.Count == 0) return;

        _isUpdatingParent = true;

        bool allChecked = Children.All(c => c.IsChecked == true);
        bool allUnchecked = Children.All(c => c.IsChecked == false);

        if (allChecked)
            IsChecked = true;
        else if (allUnchecked)
            IsChecked = false;
        else
            IsChecked = null; // indeterminate

        _isUpdatingParent = false;
    }

    /// <summary>
    /// DB 모델 기반으로 Leaf 노드 생성
    /// </summary>
    public static LayerTreeNode FromModel(IMapLayerModel model, string displayName, string iconKind = "Circle")
    {
        return new LayerTreeNode
        {
            Name = displayName,
            IconKind = iconKind,
            IsChecked = model.IsVisible,
            Opacity = model.Opacity,
            Category = model.Category ?? string.Empty,
            Model = model,
            NodeType = LayerNodeType.Leaf,
            HasOpacitySlider = model.LayerType != "Symbol"
        };
    }

    /// <summary>
    /// 그룹(부모) 노드 생성
    /// </summary>
    public static LayerTreeNode CreateGroup(string name, string iconKind = "FolderOutline", bool isExpanded = false)
    {
        return new LayerTreeNode
        {
            Name = name,
            IconKind = iconKind,
            NodeType = LayerNodeType.Group,
            IsExpanded = isExpanded,
            HasOpacitySlider = false
        };
    }

    /// <summary>
    /// 섹션(최상위) 노드 생성
    /// </summary>
    public static LayerTreeNode CreateSection(string name, string iconKind = "Layers")
    {
        return new LayerTreeNode
        {
            Name = name,
            IconKind = iconKind,
            NodeType = LayerNodeType.Section,
            IsExpanded = true,
            HasOpacitySlider = false
        };
    }

    /// <summary>
    /// 카테고리 노드 생성 (펼치면 개별 심볼 자식). 기본 접힘 — 대량 심볼 시 지연 생성(FR-02/07).
    /// </summary>
    public static LayerTreeNode CreateCategory(string name, string iconKind, string category,
        IMapLayerModel? model = null, bool isExpanded = false)
    {
        return new LayerTreeNode
        {
            Name = name,
            IconKind = iconKind,
            Category = category,
            Model = model,
            NodeType = LayerNodeType.Category,
            IsExpanded = isExpanded,
            HasOpacitySlider = false
        };
    }

    /// <summary>
    /// 개별 심볼 리프 노드 생성 (ISymbolModel 페이로드). 체크=ShowShape, opacity 없음(FR-01/03).
    /// </summary>
    public static LayerTreeNode CreateSymbolLeaf(ISymbolModel symbol, string displayName, string iconKind)
    {
        return new LayerTreeNode
        {
            Symbol = symbol,
            Name = displayName,
            IconKind = iconKind,
            IsChecked = symbol.ShowShape,
            Category = symbol.Category.ToString(),
            NodeType = LayerNodeType.Leaf,
            HasOpacitySlider = false
        };
    }

    #endregion

    #region ContextMenu Commands

    /// <summary>
    /// 삭제 가능 여부 — Overlay Leaf(Model 보유)만.
    /// 개별 심볼 리프(Symbol≠null)는 v1 비활성(파괴적, 별도 동기 경로 필요).
    /// ★ Model=null 일 때 `Model?.LayerType != "Symbol"`가 TRUE로 역전되던 버그 방지 위해 Symbol==null 가드 선행.
    /// </summary>
    public bool CanDelete => NodeType == LayerNodeType.Leaf
        && Symbol == null
        && Model?.LayerType != "Symbol";

    /// <summary>이름 바꾸기 가능 여부 — Overlay Leaf만(개별 심볼 v1 비활성).</summary>
    public bool CanRename => CanDelete;

    /// <summary>위로 이동 가능 여부 — Parent.Children 내 첫 번째가 아닌 경우</summary>
    public bool CanMoveUp => Parent != null
        && Parent.Children.IndexOf(this) > 0;

    /// <summary>아래로 이동 가능 여부 — Parent.Children 내 마지막이 아닌 경우</summary>
    public bool CanMoveDown => Parent != null
        && Parent.Children.IndexOf(this) < Parent.Children.Count - 1;

    /// <summary>이벤트를 LayerPanelControl로 라우팅하기 위한 참조</summary>
    internal Action<LayerTreeNode>? OnDeleteAction { get; set; }
    internal Action<LayerTreeNode>? OnMoveUpAction { get; set; }
    internal Action<LayerTreeNode>? OnMoveDownAction { get; set; }
    internal Action<LayerTreeNode, string>? OnRenameAction { get; set; }
    internal Action<LayerTreeNode>? OnNavigateAction { get; set; }

    public ICommand DeleteCommand => _deleteCommand ??=
        new RelayCommand(_ => OnDeleteAction?.Invoke(this), _ => CanDelete);

    public ICommand MoveUpCommand => _moveUpCommand ??=
        new RelayCommand(_ => OnMoveUpAction?.Invoke(this), _ => CanMoveUp);

    public ICommand MoveDownCommand => _moveDownCommand ??=
        new RelayCommand(_ => OnMoveDownAction?.Invoke(this), _ => CanMoveDown);

    public ICommand RenameCommand => _renameCommand ??=
        new RelayCommand(_ =>
        {
            _editingName = Name;
            IsEditing = true;
        }, _ => CanRename);

    public ICommand CommitRenameCommand => _commitRenameCommand ??=
        new RelayCommand(_ =>
        {
            IsEditing = false;
            if (string.IsNullOrWhiteSpace(Name))
            {
                Name = _editingName;
                return;
            }
            if (Name != _editingName)
                OnRenameAction?.Invoke(this, Name);
        });

    public ICommand CancelRenameCommand => _cancelRenameCommand ??=
        new RelayCommand(_ =>
        {
            Name = _editingName;
            IsEditing = false;
        });

    public ICommand NavigateCommand => _navigateCommand ??=
        new RelayCommand(
            _ => OnNavigateAction?.Invoke(this),
            // Overlay Leaf(Model 보유) 또는 개별 심볼 리프(Symbol 보유)는 navigate 가능. 카테고리 토글 전용 Leaf는 불가.
            _ => NodeType == LayerNodeType.Leaf && (Symbol != null || Model?.LayerType != "Symbol"));

    #endregion

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    #endregion
}

public enum LayerNodeType
{
    Section,  // 최상위: OVERLAY MAP, OVERLAY IMAGE, SYMBOLS
    Group,    // 중간: PIDS 장비
    Category, // 카테고리: 카메라/센서/군사 등 — 펼치면 개별 심볼 자식(FR-01)
    Leaf      // 개별 항목: 오버레이 아이템 / 개별 심볼(IsSymbolLeaf=true)
}
