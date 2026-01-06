// Custom navigation active state handler for Admin Area
// This completely disables misc.js behavior for sidebar navigation
(function($) {
  'use strict';
  
  // Function to normalize paths for comparison
  function normalizePath(path) {
    if (!path) return '';
    // Remove query string, hash, and trailing slash
    path = path.split('?')[0].split('#')[0];
    path = path.replace(/\/$/, '') || '/';
    return path;
  }
  
  // Function to check if a link should be active (exact matching only)
  function shouldBeActive(linkHref, currentPath) {
    if (!linkHref || linkHref.startsWith('#')) return false;
    
    var normalizedHref = normalizePath(linkHref);
    var normalizedCurrent = normalizePath(currentPath);
    
    // Exact match
    if (normalizedHref === normalizedCurrent) {
      return true;
    }
    
    // Special case: Dashboard - ONLY match when current is EXACTLY /Admin or /Admin/Index
    // Dashboard should NEVER be active when on other pages
    if (normalizedHref === '/Admin' || normalizedHref === '/Admin/Index') {
      // Only active if current path is exactly Dashboard
      return normalizedCurrent === '/Admin' || normalizedCurrent === '/Admin/Index';
    }
    
    // For other pages: /Admin/Users should match /Admin/Users/Details
    // But NOT /Admin should match /Admin/Users (Dashboard should not be active)
    if (normalizedCurrent.startsWith(normalizedHref + '/')) {
      // Ensure it's a complete segment match
      // Example: /Admin/Users matches /Admin/Users/Details
      // But /Admin does NOT match /Admin/Users (handled above)
      return true;
    }
    
    return false;
  }
  
  // Function to apply correct active state - ONLY ONE ITEM ACTIVE AT A TIME
  function applyCorrectActiveState() {
    var sidebar = $('.sidebar');
    if (!sidebar.length) return;
    
    // Get current path
    var currentPath = location.pathname;
    
    // CRITICAL: Remove ALL active states first (including from misc.js)
    $('.nav li', sidebar).removeClass('active');
    $('.nav li a', sidebar).removeClass('active');
    $('.collapse', sidebar).removeClass('show');
    
    // Find the ONE correct nav item to activate
    var foundActive = false;
    $('.nav li a', sidebar).each(function() {
      var $this = $(this);
      var href = $this.attr('href');
      
      // Skip collapse toggles and empty hrefs
      if (!href || href.startsWith('#')) {
        return;
      }
      
      // Check if this link should be active
      if (shouldBeActive(href, currentPath)) {
        // Only activate if we haven't found one yet (prevent duplicates)
        if (!foundActive) {
          foundActive = true;
          
          // Add active to nav-item
          $this.parents('.nav-item').last().addClass('active');
          $this.addClass('active');
          
          // If it's in a sub-menu, expand the parent collapse
          if ($this.parents('.sub-menu').length) {
            var $collapse = $this.closest('.collapse');
            if ($collapse.length) {
              $collapse.addClass('show');
              // Also activate the parent menu item (Game Management)
              $collapse.siblings('.nav-link').parent('.nav-item').addClass('active');
            }
          }
        }
      }
    });
  }
  
  // Disable misc.js behavior for sidebar BEFORE it runs
  $(document).ready(function() {
    var sidebar = $('.sidebar');
    
    // Clear all active states IMMEDIATELY
    $('.nav li', sidebar).removeClass('active');
    $('.nav li a', sidebar).removeClass('active');
    $('.collapse', sidebar).removeClass('show');
    
    // Apply correct logic immediately
    applyCorrectActiveState();
  });
  
  // Completely override misc.js behavior for sidebar
  $(function() {
    // Apply our logic immediately
    applyCorrectActiveState();
    
    // Continuously monitor and fix active states (override misc.js)
    // This ensures misc.js can't set wrong active states
    var monitorInterval = setInterval(function() {
      var sidebar = $('.sidebar');
      if (!sidebar.length) {
        clearInterval(monitorInterval);
        return;
      }
      
      // Check if misc.js has set wrong active states
      var activeCount = $('.nav li.active', sidebar).length;
      if (activeCount > 1) {
        // misc.js has set multiple active states - fix it
        applyCorrectActiveState();
      }
    }, 50); // Check every 50ms
    
    // Stop monitoring after 2 seconds (misc.js should be done by then)
    setTimeout(function() {
      clearInterval(monitorInterval);
      // Final apply
      applyCorrectActiveState();
    }, 2000);
    
    // Re-apply on navigation
    $(window).on('popstate', function() {
      setTimeout(applyCorrectActiveState, 10);
    });
    
    // Re-apply when clicking nav links (before page navigation)
    $(document).on('click', '.sidebar .nav li a[href]:not([href^="#"])', function(e) {
      // Don't prevent navigation, just fix active state
      setTimeout(applyCorrectActiveState, 10);
    });
  });
  
  // Expose function for manual calls
  window.applyAdminNavActiveState = applyCorrectActiveState;
})(jQuery);

