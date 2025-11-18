/**
 * Category Tree Management
 * Handles expand/collapse and tree interactions for category management
 */

const CategoryTree = (function() {
    'use strict';

    let initialized = false;

    /**
     * Initialize the category tree
     */
    function init() {
        if (initialized) return;
        
        setupExpandCollapse();
        setupHoverEffects();
        
        initialized = true;
        console.log('Category tree initialized');
    }

    /**
     * Setup expand/collapse functionality
     */
    function setupExpandCollapse() {
        document.addEventListener('click', function(e) {
            const expandBtn = e.target.closest('.category-expand-btn');
            if (!expandBtn) return;

            e.preventDefault();
            e.stopPropagation();

            const categoryNode = expandBtn.closest('.category-node');
            const childrenContainer = categoryNode.querySelector('.category-children');
            const icon = expandBtn.querySelector('i');
            const isExpanded = expandBtn.getAttribute('data-expanded') === 'true';

            if (isExpanded) {
                // Collapse
                childrenContainer.style.display = 'none';
                icon.classList.remove('bi-chevron-down');
                icon.classList.add('bi-chevron-right');
                expandBtn.setAttribute('data-expanded', 'false');
            } else {
                // Expand
                childrenContainer.style.display = 'block';
                icon.classList.remove('bi-chevron-right');
                icon.classList.add('bi-chevron-down');
                expandBtn.setAttribute('data-expanded', 'true');
            }
        });
    }

    /**
     * Setup hover effects for better UX
     */
    function setupHoverEffects() {
        document.addEventListener('mouseenter', function(e) {
            const categoryItem = e.target.closest('.category-item');
            if (!categoryItem) return;

            categoryItem.style.backgroundColor = '#f8f9fa';
        }, true);

        document.addEventListener('mouseleave', function(e) {
            const categoryItem = e.target.closest('.category-item');
            if (!categoryItem) return;

            categoryItem.style.backgroundColor = '';
        }, true);
    }

    /**
     * Expand all categories
     */
    function expandAll() {
        document.querySelectorAll('.category-expand-btn').forEach(function(btn) {
            const categoryNode = btn.closest('.category-node');
            const childrenContainer = categoryNode.querySelector('.category-children');
            const icon = btn.querySelector('i');

            if (childrenContainer) {
                childrenContainer.style.display = 'block';
                icon.classList.remove('bi-chevron-right');
                icon.classList.add('bi-chevron-down');
                btn.setAttribute('data-expanded', 'true');
            }
        });
    }

    /**
     * Collapse all categories
     */
    function collapseAll() {
        document.querySelectorAll('.category-expand-btn').forEach(function(btn) {
            const categoryNode = btn.closest('.category-node');
            const childrenContainer = categoryNode.querySelector('.category-children');
            const icon = btn.querySelector('i');

            if (childrenContainer) {
                childrenContainer.style.display = 'none';
                icon.classList.remove('bi-chevron-down');
                icon.classList.add('bi-chevron-right');
                btn.setAttribute('data-expanded', 'false');
            }
        });
    }

    /**
     * Expand to a specific category (useful for deep linking)
     */
    function expandToCategory(categoryId) {
        const targetNode = document.querySelector(`[data-category-id="${categoryId}"]`);
        if (!targetNode) return;

        // Find all parent nodes and expand them
        let currentNode = targetNode.parentElement;
        while (currentNode) {
            if (currentNode.classList.contains('category-children')) {
                const parentNode = currentNode.previousElementSibling;
                if (parentNode) {
                    const expandBtn = parentNode.querySelector('.category-expand-btn');
                    if (expandBtn) {
                        const icon = expandBtn.querySelector('i');
                        currentNode.style.display = 'block';
                        icon.classList.remove('bi-chevron-right');
                        icon.classList.add('bi-chevron-down');
                        expandBtn.setAttribute('data-expanded', 'true');
                    }
                }
            }
            currentNode = currentNode.parentElement;
        }

        // Scroll to and highlight the target
        targetNode.scrollIntoView({ behavior: 'smooth', block: 'center' });
        const categoryItem = targetNode.querySelector('.category-item');
        if (categoryItem) {
            categoryItem.style.backgroundColor = '#fff3cd';
            setTimeout(function() {
                categoryItem.style.backgroundColor = '';
            }, 2000);
        }
    }

    /**
     * Get category statistics
     */
    function getStats() {
        const allNodes = document.querySelectorAll('.category-node');
        const rootNodes = document.querySelectorAll('.category-tree > .category-node');
        
        let maxDepth = 0;
        allNodes.forEach(function(node) {
            const level = parseInt(node.getAttribute('data-level') || '0');
            if (level > maxDepth) {
                maxDepth = level;
            }
        });

        return {
            total: allNodes.length,
            root: rootNodes.length,
            maxDepth: maxDepth
        };
    }

    // Public API
    return {
        init: init,
        expandAll: expandAll,
        collapseAll: collapseAll,
        expandToCategory: expandToCategory,
        getStats: getStats
    };
})();

// Auto-initialize if DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function() {
        if (document.querySelector('.category-tree')) {
            CategoryTree.init();
        }
    });
} else {
    if (document.querySelector('.category-tree')) {
        CategoryTree.init();
    }
}
